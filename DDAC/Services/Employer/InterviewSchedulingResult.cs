using DDAC.Models;

namespace DDAC.Services.Employer;

// Local operation result, not an HTTP/transport contract.
public sealed record InterviewSchedulingResult(
    JobApplication? Application,
    JobVacancy? Job,
    IReadOnlyDictionary<string, string> Errors,
    bool Scheduled);
