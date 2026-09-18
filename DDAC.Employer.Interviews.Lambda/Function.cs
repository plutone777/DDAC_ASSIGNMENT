using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using DDAC.Contracts.Employer;
using DDAC.Models;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace DDAC.Employer.Interviews.Lambda;

public sealed class Function
{
    private readonly Func<string?> callerKey;
    private readonly Func<ISchedulingInvocation> createInvocation;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Function() : this(
        () => Environment.GetEnvironmentVariable("DDAC_INTERVIEWS_CALLER_KEY"),
        () => new SchedulingInvocation()) { }

    internal Function(Func<string?> callerKey, Func<ISchedulingInvocation> createInvocation)
    {
        this.callerKey = callerKey;
        this.createInvocation = createInvocation;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(
        APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        try
        {
            var expectedKey = callerKey();
            if (string.IsNullOrWhiteSpace(expectedKey) || expectedKey.Length < 32 || expectedKey.Any(char.IsControl))
                return Failure(500, "internal_error", "Scheduling service is unavailable.");
            var suppliedKey = Header(request, "X-Caller-Key");
            if (suppliedKey is null || !CryptographicOperations.FixedTimeEquals(
                    SHA256.HashData(Encoding.UTF8.GetBytes(suppliedKey)),
                    SHA256.HashData(Encoding.UTF8.GetBytes(expectedKey))))
                return Unauthorized();

            // Only the authenticated server caller may assert identity; never use JSON ownership fields.
            if (!int.TryParse(Header(request, "X-Employer-ID"), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var employerId) || employerId <= 0)
                return Unauthorized();

            ScheduleInterviewRequest? input;
            try
            {
                var body = request.IsBase64Encoded
                    ? Encoding.UTF8.GetString(Convert.FromBase64String(request.Body ?? ""))
                    : request.Body;
                input = JsonSerializer.Deserialize<ScheduleInterviewRequest>(body ?? "", Json);
            }
            catch (Exception error) when (error is JsonException or FormatException)
            {
                return Failure(400, ScheduleInterviewResponse.ValidationFailed, "Invalid scheduling request.");
            }
            if (input is null)
                return Failure(400, ScheduleInterviewResponse.ValidationFailed, "A scheduling request is required.");

            var validation = new List<ValidationResult>();
            Validator.TryValidateObject(input, new ValidationContext(input), validation, true);
            if (validation.Count > 0)
                return Respond(400, new ScheduleInterviewResponse
                {
                    Success = false, ErrorCode = ScheduleInterviewResponse.ValidationFailed,
                    Errors = validation.SelectMany(error => error.MemberNames.DefaultIfEmpty("")
                            .Select(field => new { Field = JsonNamingPolicy.CamelCase.ConvertName(field),
                                Message = error.ErrorMessage ?? "Invalid value." }))
                        .GroupBy(error => error.Field)
                        .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray())
                });

            // A fresh context and service per invocation; disposal also runs on failures.
            await using var invocation = createInvocation();
            if (!await invocation.IsActiveEmployerAsync(employerId)) return Unauthorized();
            var interview = new JobInterview
            {
                ApplicationID = input.ApplicationID!.Value, InterviewDate = input.InterviewDate!.Value,
                InterviewType = input.InterviewType!, Location = input.Location!, Notes = input.Notes!
            };
            var result = await invocation.Service.ScheduleAsync(employerId, interview);
            if (result.Application is null)
                return Failure(404, ScheduleInterviewResponse.NotFound, "Application not found.");
            if (!result.Scheduled)
                return Respond(400, new ScheduleInterviewResponse
                {
                    Success = false, ErrorCode = ScheduleInterviewResponse.ValidationFailed,
                    Errors = result.Errors.ToDictionary(error => JsonNamingPolicy.CamelCase.ConvertName(error.Key),
                        error => new[] { error.Value })
                });
            return Respond(200, new ScheduleInterviewResponse
            {
                Success = true, InterviewID = interview.InterviewID, ApplicationStatus = result.Application.Status
            });
        }
        catch (Exception)
        {
            // No exception message, event body, headers, credentials or connection details leave the handler.
            return Failure(500, "internal_error", "Scheduling service is unavailable.");
        }
    }

    private static string? Header(APIGatewayHttpApiV2ProxyRequest request, string name)
    {
        var matches = request.Headers?.Where(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches is { Length: 1 } ? matches[0].Value : null;
    }

    private static APIGatewayHttpApiV2ProxyResponse Unauthorized() =>
        Failure(401, "unauthorized", "Trusted caller authentication required.");

    private static APIGatewayHttpApiV2ProxyResponse Failure(int status, string code, string message) =>
        Respond(status, new ScheduleInterviewResponse
        {
            Success = false, ErrorCode = code, Errors = new Dictionary<string, string[]> { [""] = [message] }
        });

    private static APIGatewayHttpApiV2ProxyResponse Respond(int status, ScheduleInterviewResponse result) => new()
    {
        StatusCode = status, Body = JsonSerializer.Serialize(result, Json), IsBase64Encoded = false,
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
    };
}
