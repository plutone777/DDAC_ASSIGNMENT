using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using DDAC.Contracts.Employer;
using Xunit;

namespace DDAC.Employer.Tests;

// Pure DTO tests: no application host, fixture, database or network access.
public sealed class InterviewContractTests
{
    [Fact]
    public void Contract_checks_stop_at_first_failure()
    {
        var request = new ScheduleInterviewRequest
        {
            ApplicationID = 42,
            InterviewDate = new DateTime(2030, 1, 2, 10, 30, 0, DateTimeKind.Unspecified),
            InterviewType = "Online", Location = "LOCAL TEST meeting", Notes = "LOCAL TEST notes"
        };
        var json = JsonSerializer.Serialize(request);
        Assert.Equal(request, JsonSerializer.Deserialize<ScheduleInterviewRequest>(json));
        Assert.Empty(Validate(request));
        Assert.Equal(DateTimeKind.Unspecified,
            JsonSerializer.Deserialize<ScheduleInterviewRequest>(json)!.InterviewDate!.Value.Kind);

        var missing = Validate(JsonSerializer.Deserialize<ScheduleInterviewRequest>("{}")!);
        Assert.Equal(new[] { "ApplicationID", "InterviewDate", "InterviewType" },
            missing.SelectMany(x => x.MemberNames).OrderBy(x => x).ToArray());
        Assert.Contains(Validate(request with { ApplicationID = 0 }), x => x.MemberNames.Contains("ApplicationID"));
        Assert.Contains(Validate(request with { ApplicationID = -1 }), x => x.MemberNames.Contains("ApplicationID"));
        Assert.Contains(Validate(request with { InterviewType = " " }), x => x.MemberNames.Contains("InterviewType"));
        Assert.Contains(Validate(request with { InterviewType = new string('x', 31) }), x => x.MemberNames.Contains("InterviewType"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ScheduleInterviewRequest>("{\"interviewDate\":\"invalid\"}"));
        Assert.Empty(Validate(request with { Location = null, Notes = null }));

        // The exact allow-list excludes EmployerID, workflow status and navigation data.
        using var document = JsonDocument.Parse(json);
        Assert.Equal(new[] { "applicationID", "interviewDate", "interviewType", "location", "notes" },
            document.RootElement.EnumerateObject().Select(x => x.Name).ToArray());
        Assert.Equal(5, typeof(ScheduleInterviewRequest).GetProperties().Length);
        var spoofed = JsonSerializer.Deserialize<ScheduleInterviewRequest>(json[..^1] + ",\"employerID\":999}")!;
        Assert.Equal(request, spoofed); // Unknown input supplies no identity to this DTO.

        var success = new ScheduleInterviewResponse
        {
            Success = true, InterviewID = 7, ApplicationStatus = "Shortlisted"
        };
        Assert.Equal("{\"success\":true,\"interviewID\":7,\"applicationStatus\":\"Shortlisted\",\"errorCode\":null,\"errors\":{}}",
            JsonSerializer.Serialize(success));
        var roundTrip = JsonSerializer.Deserialize<ScheduleInterviewResponse>(JsonSerializer.Serialize(success))!;
        Assert.True(roundTrip.Success);
        Assert.Equal(7, roundTrip.InterviewID);
        Assert.Equal("Shortlisted", roundTrip.ApplicationStatus);
        Assert.Null(roundTrip.ErrorCode);
        Assert.Empty(roundTrip.Errors);

        var validation = new ScheduleInterviewResponse
        {
            Success = false, ErrorCode = ScheduleInterviewResponse.ValidationFailed,
            Errors = new Dictionary<string, string[]> { ["interviewDate"] = ["Interview date must be in the future."] }
        };
        var failure = JsonSerializer.Deserialize<ScheduleInterviewResponse>(JsonSerializer.Serialize(validation))!;
        Assert.False(failure.Success);
        Assert.Equal("validation_failed", failure.ErrorCode);
        Assert.Null(failure.InterviewID);
        Assert.Null(failure.ApplicationStatus);
        Assert.Equal(new[] { "Interview date must be in the future." }, failure.Errors["interviewDate"]);

        // Both outcomes deliberately have the same public representation.
        static ScheduleInterviewResponse Unavailable() => new()
        {
            Success = false, ErrorCode = ScheduleInterviewResponse.NotFound,
            Errors = new Dictionary<string, string[]> { [""] = ["Application not found."] }
        };
        var ownershipDenied = Unavailable();
        var notFound = Unavailable();
        Assert.Equal(JsonSerializer.Serialize(notFound), JsonSerializer.Serialize(ownershipDenied));
        Assert.Equal("{\"success\":false,\"interviewID\":null,\"applicationStatus\":null,\"errorCode\":\"not_found\",\"errors\":{\"\":[\"Application not found.\"]}}",
            JsonSerializer.Serialize(notFound));
    }

    private static List<ValidationResult> Validate(ScheduleInterviewRequest request)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);
        return errors;
    }
}
