using DDAC.Data;
using DDAC.Models;
using Microsoft.EntityFrameworkCore;

namespace DDAC.Services.Employer;

public sealed class InterviewSchedulingService(ApplicationDbContext context) : IInterviewSchedulingService
{
    public IReadOnlyList<string> InterviewTypes { get; } = Array.AsReadOnly(new[] { "On-site", "Online", "Phone" });

    public async Task<InterviewSchedulingResult> ScheduleAsync(int employerId, JobInterview input, bool inputIsValid = true)
    {
        var owned = await (
            from application in context.JobApplications
            join job in context.JobVacancies on application.JobID equals job.JobID
            where application.ApplicationID == input.ApplicationID && job.EmployerID == employerId
            select new { Application = application, Job = job }).FirstOrDefaultAsync();

        var errors = new Dictionary<string, string>();
        if (owned is null)
            return new(null, null, errors, false);

        input.Location = input.Location?.Trim() ?? string.Empty;
        input.Notes = input.Notes?.Trim() ?? string.Empty;
        var normalizedType = InterviewTypes.FirstOrDefault(type =>
            string.Equals(type, input.InterviewType, StringComparison.OrdinalIgnoreCase));
        if (normalizedType is null)
            errors[nameof(input.InterviewType)] = "Select a valid interview type.";
        else
            input.InterviewType = normalizedType;

        if (input.InterviewDate <= DateTime.Now)
            errors[nameof(input.InterviewDate)] = "Interview date must be in the future.";

        if (input.InterviewType == "On-site" && string.IsNullOrWhiteSpace(input.Location))
            errors[nameof(input.Location)] = "Enter a location for an on-site interview.";

        if (!inputIsValid || errors.Count != 0)
            return new(owned.Application, owned.Job, errors, false);

        input.Status = "Scheduled";
        context.JobInterviews.Add(input);
        if (owned.Application.Status is "Submitted" or "Under Review")
            owned.Application.Status = "Shortlisted";

        // Keep the original single SaveChanges boundary for interview + status transition.
        await context.SaveChangesAsync();
        return new(owned.Application, owned.Job, errors, true);
    }
}
