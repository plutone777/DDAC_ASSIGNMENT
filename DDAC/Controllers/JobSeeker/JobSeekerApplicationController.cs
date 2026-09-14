using DDAC.Data;
using DDAC.Models;
using DDAC.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DDAC.Controllers
{
    public class JobSeekerApplicationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly JobApplicationService _jobApplicationService;

        public JobSeekerApplicationController(
            ApplicationDbContext context,
            JobApplicationService jobApplicationService)
        {
            _context = context;
            _jobApplicationService = jobApplicationService;
        }

        [HttpGet]
        public async Task<IActionResult> Apply(int jobId)
        {
            var jobSeekerId = GetCurrentJobSeekerId();

            if (jobSeekerId == null)
            {
                return RedirectToAction("Login", "User");
            }

            var job = await _context.JobVacancies
                .FirstOrDefaultAsync(j => j.JobID == jobId);

            if (job == null)
            {
                return NotFound();
            }

            var profile = await _context.JobSeekerProfiles
                .FirstOrDefaultAsync(p =>
                    p.JobSeekerID == jobSeekerId.Value);

            if (profile == null)
            {
                TempData["ApplicationError"] =
                    "Please complete your job seeker profile before applying.";

                return RedirectToAction(
                    "EditProfile",
                    "JobSeekerProfile");
            }

            if (string.IsNullOrWhiteSpace(profile.ResumeURL))
            {
                TempData["ApplicationError"] =
                    "You must upload a resume to your profile before applying for a job.";

                return RedirectToAction(
                    "EditProfile",
                    "JobSeekerProfile");
            }

            var alreadyApplied = await _context.JobApplications
                .AnyAsync(a =>
                    a.JobID == jobId &&
                    a.JobSeekerID == jobSeekerId.Value);

            if (alreadyApplied)
            {
                TempData["ApplicationError"] =
                    "You have already applied for this job.";

                return RedirectToAction(
                    "JobDetails",
                    "JobSeeker",
                    new { id = jobId });
            }

            ViewBag.Job = job;
            ViewBag.ResumeURL = profile.ResumeURL;

            return View(
                "~/Views/JobSeeker/ApplyJob.cshtml",
                new JobApplication
                {
                    JobID = jobId,
                    JobSeekerID = jobSeekerId.Value
                });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(
            int jobId,
            string coverLetter)
        {
            var jobSeekerId = GetCurrentJobSeekerId();

            if (jobSeekerId == null)
            {
                return RedirectToAction("Login", "User");
            }

            var result = await _jobApplicationService.SubmitApplicationAsync(
                jobSeekerId.Value,
                jobId,
                coverLetter);

            if (!result.Success)
            {
                if (string.IsNullOrWhiteSpace(coverLetter))
                {
                    var job = await _context.JobVacancies
                        .FirstOrDefaultAsync(j => j.JobID == jobId);

                    var profile = await _context.JobSeekerProfiles
                        .FirstOrDefaultAsync(p =>
                            p.JobSeekerID == jobSeekerId.Value);

                    if (job == null)
                    {
                        return NotFound();
                    }

                    ViewBag.Job = job;
                    ViewBag.ResumeURL = profile?.ResumeURL;

                    ModelState.AddModelError(
                        "CoverLetter",
                        result.Message);

                    return View(
                        "~/Views/JobSeeker/ApplyJob.cshtml",
                        new JobApplication
                        {
                            JobID = jobId,
                            JobSeekerID = jobSeekerId.Value,
                            CoverLetter = coverLetter
                        });
                }

                TempData["ApplicationError"] = result.Message;

                return RedirectToAction(
                    "JobDetails",
                    "JobSeeker",
                    new { id = jobId });
            }

            TempData["ApplicationSuccess"] =
                "Your application has been submitted successfully.";

            return RedirectToAction(
                "JobDetails",
                "JobSeeker",
                new { id = jobId });
        }

        private int? GetCurrentJobSeekerId()
        {
            return HttpContext.Session.GetInt32("UserID");
        }
    }
}
