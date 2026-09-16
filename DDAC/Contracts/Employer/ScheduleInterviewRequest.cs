using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DDAC.Contracts.Employer;

// Business input only. Caller identity must come from authenticated server context.
// Transport contract for interview scheduling; Employer identity is supplied separately by the trusted server-side caller.
public sealed record ScheduleInterviewRequest
{
    [JsonPropertyName("applicationID")]
    [Required, Range(1, int.MaxValue)]
    public int? ApplicationID { get; init; }

    // Preserve the existing local wall-clock value; no UTC conversion is implied.
    // Cross-machine deployment requires an explicit timezone policy.
    [JsonPropertyName("interviewDate")]
    [Required]
    public DateTime? InterviewDate { get; init; }

    [JsonPropertyName("interviewType")]
    [Required, StringLength(30)]
    public string? InterviewType { get; init; }

    [JsonPropertyName("location")]
    public string? Location { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}
