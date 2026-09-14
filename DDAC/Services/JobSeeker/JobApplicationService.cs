using DDAC.Data;
using DDAC.Models;
using Microsoft.EntityFrameworkCore;

namespace DDAC.Services
{
    public class JobApplicationService
    {
        private readonly ApplicationDbContext _context;

        public JobApplicationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Success, string Message, JobApplication? Application)>
            SubmitApplicationAsync(
                int jobSeekerId,
                int jobId,
                string coverLetter)
        {
            var job = await _context.JobVacancies
                .FirstOrDefaultAsync(j => j.JobID == jobId);

            if (job == null)
            {
                return (false, "Job vacancy not found.", null);
            }

            var profile = await _context.JobSeekerProfiles
                .FirstOrDefaultAsync(p => p.JobSeekerID == jobSeekerId);

            if (profile == null)
            {
                return (false,
                    "Please complete your job seeker profile before applying.",
                    null);
            }

            if (string.IsNullOrWhiteSpace(profile.ResumeURL))
            {
                return (false,
                    "You must upload a resume to your profile before applying.",
                    null);
            }

            var alreadyApplied = await _context.JobApplications
                .AnyAsync(a =>
                    a.JobID == jobId &&
                    a.JobSeekerID == jobSeekerId);

            if (alreadyApplied)
            {
                return (false,
                    "You have already applied for this job.",
                    null);
            }

            if (string.IsNullOrWhiteSpace(coverLetter))
            {
                return (false,
                    "Please write a cover letter before submitting your application.",
                    null);
            }

            var application = new JobApplication
            {
                JobID = jobId,
                JobSeekerID = jobSeekerId,
                ApplicationDate = DateTime.Now,
                ResumeURL = profile.ResumeURL,
                CoverLetter = coverLetter.Trim(),
                Status = "Submitted"
            };

            _context.JobApplications.Add(application);

            await _context.SaveChangesAsync();

            return (
                true,
                "Application submitted successfully.",
                application
            );
        }
    }
}