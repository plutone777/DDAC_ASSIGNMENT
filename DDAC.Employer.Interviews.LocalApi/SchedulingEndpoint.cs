using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DDAC.Contracts.Employer;
using DDAC.Data;
using DDAC.Models;
using DDAC.Services.Employer;
using Microsoft.EntityFrameworkCore;

namespace DDAC.Employer.Interviews.LocalApi;

internal static class SchedulingEndpoint
{
    internal static ScheduleInterviewResponse Failure(string code, string message) => new()
    {
        Success = false, ErrorCode = code,
        Errors = new Dictionary<string, string[]> { [""] = [message] }
    };

    internal static async Task<IResult> HandleAsync(HttpContext http, ApplicationDbContext db,
        IInterviewSchedulingService service, string secret)
    {
        var key = http.Request.Headers["X-Caller-Key"];
        if (key.Count != 1 || !CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(key.ToString())),
            SHA256.HashData(Encoding.UTF8.GetBytes(secret))))
            return Results.Json(Failure("unauthorized", "Trusted caller authentication required."), statusCode: 401);

        // This header is a trusted server assertion ONLY because the caller key was verified.
        // Never derive it from request JSON, or expose the key to browser JavaScript.
        var identity = http.Request.Headers["X-Employer-ID"];
        if (identity.Count != 1 || !int.TryParse(identity.ToString(), NumberStyles.None,
                CultureInfo.InvariantCulture, out var employerId) || employerId <= 0)
            return Results.Json(Failure("unauthorized", "Trusted caller authentication required."), statusCode: 401);
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.UserID == employerId && x.Role == "Employer" && x.Status == "Active"))
            return Results.Json(Failure("unauthorized", "Trusted caller authentication required."), statusCode: 401);

        ScheduleInterviewRequest? request;
        try
        {
            // Manual parsing keeps malformed body errors generic and runs AFTER authentication.
            if (!http.Request.HasJsonContentType())
                return Results.Json(Failure(ScheduleInterviewResponse.ValidationFailed, "A JSON scheduling request is required."), statusCode: 400);
            request = await http.Request.ReadFromJsonAsync<ScheduleInterviewRequest>();
        }
        catch (JsonException)
        {
            return Results.Json(Failure(ScheduleInterviewResponse.ValidationFailed, "Invalid scheduling request."), statusCode: 400);
        }
        if (request is null)
            return Results.Json(Failure(ScheduleInterviewResponse.ValidationFailed, "A scheduling request is required."), statusCode: 400);

        var validation = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), validation, validateAllProperties: true);
        if (validation.Count > 0)
        {
            var errors = validation.SelectMany(error => error.MemberNames.DefaultIfEmpty("")
                .Select(field => new { Field = JsonNamingPolicy.CamelCase.ConvertName(field), Message = error.ErrorMessage ?? "Invalid value." }))
                .GroupBy(error => error.Field).ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray());
            return Results.Json(new ScheduleInterviewResponse
                { Success = false, ErrorCode = ScheduleInterviewResponse.ValidationFailed, Errors = errors }, statusCode: 400);
        }

        // The current MVC form sends local wall-clock time without an offset.
        // Preserve Unspecified/Local DateTime semantics; do not introduce UTC conversion.
        var input = new JobInterview
        {
            ApplicationID = request.ApplicationID!.Value, InterviewDate = request.InterviewDate!.Value,
            InterviewType = request.InterviewType!, Location = request.Location!, Notes = request.Notes!
        };
        var result = await service.ScheduleAsync(employerId, input);
        if (result.Application is null)
            return Results.Json(Failure(ScheduleInterviewResponse.NotFound, "Application not found."), statusCode: 404);
        if (!result.Scheduled)
            return Results.Json(new ScheduleInterviewResponse
            {
                Success = false, ErrorCode = ScheduleInterviewResponse.ValidationFailed,
                Errors = result.Errors.ToDictionary(error => JsonNamingPolicy.CamelCase.ConvertName(error.Key), error => new[] { error.Value })
            }, statusCode: 400);
        return Results.Json(new ScheduleInterviewResponse
            { Success = true, InterviewID = input.InterviewID, ApplicationStatus = result.Application.Status }, statusCode: 200);
    }
}
