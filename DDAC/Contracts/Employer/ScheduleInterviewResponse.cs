using System.Text.Json.Serialization;

namespace DDAC.Contracts.Employer;

// Transport data only: never include EF entities, exception details or caller secrets.
// The API host maps the in-process scheduling result to this transport response and HTTP status.
public sealed record ScheduleInterviewResponse
{
    public const string ValidationFailed = "validation_failed";
    // Use the same code/message for missing applications and ownership denial.
    public const string NotFound = "not_found";

    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("interviewID")]
    public int? InterviewID { get; init; }

    [JsonPropertyName("applicationStatus")]
    public string? ApplicationStatus { get; init; }

    // Null on success; stable machine-readable category on failure.
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    // Keys use request JSON field names; an empty key denotes a non-field error.
    // Messages must be safe for the caller, not raw exception text.
    [JsonPropertyName("errors")]
    public IReadOnlyDictionary<string, string[]> Errors { get; init; }
        = new Dictionary<string, string[]>();
}
