using DDAC.Contracts.Employer;

namespace DDAC.Services.Employer;

public interface IInterviewApiClient
{
    Task<InterviewApiResult> ScheduleAsync(int trustedEmployerId, ScheduleInterviewRequest request);
}

// MVC-only result, not a wire contract. Null Response means no usable business response.
public sealed record InterviewApiResult(ScheduleInterviewResponse? Response, bool Unavailable = false);
