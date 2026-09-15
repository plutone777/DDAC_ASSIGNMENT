using DDAC.Models;

namespace DDAC.Services.Employer;

public interface IInterviewSchedulingService
{
    IReadOnlyList<string> InterviewTypes { get; }

    // employerId must come from the authenticated server context, never form ownership fields.
    // inputIsValid preserves MVC binding/annotation failures without depending on MVC ModelState.
    Task<InterviewSchedulingResult> ScheduleAsync(int employerId, JobInterview input, bool inputIsValid = true);
}
