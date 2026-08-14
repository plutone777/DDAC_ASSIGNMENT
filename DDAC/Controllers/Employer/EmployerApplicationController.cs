using DDAC.Data;
using DDAC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DDAC.Controllers.Employer;

public class EmployerApplicationController : EmployerControllerBase
{
    private static readonly IReadOnlyList<string> InterviewTypes = ["On-site", "Online", "Phone"];
    private readonly ApplicationDbContext _context;

    public EmployerApplicationController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Applications(string? status)
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        var jobs = await _context.JobVacancies
            .AsNoTracking()
            .Where(job => job.EmployerID == employerId.Value)
            .ToListAsync();
        var jobIds = jobs.Select(job => job.JobID).ToList();

        var query = _context.JobApplications
            .AsNoTracking()
            .Where(application => jobIds.Contains(application.JobID));

        if (!string.IsNullOrWhiteSpace(status) && ApplicationStatuses.Contains(status))
        {
            query = query.Where(application => application.Status == status);
        }

        var applications = await query
            .OrderByDescending(application => application.ApplicationDate)
            .ToListAsync();
        var applicantIds = applications.Select(application => application.JobSeekerID).Distinct().ToList();

        ViewBag.JobsById = jobs.ToDictionary(job => job.JobID);
        ViewBag.ApplicantsById = await _context.Users
            .AsNoTracking()
            .Where(user => applicantIds.Contains(user.UserID))
            .ToDictionaryAsync(user => user.UserID);
        ViewBag.Statuses = ApplicationStatuses;
        ViewBag.SelectedStatus = status;

        return View(applications);
    }

    [HttpGet]
    public async Task<IActionResult> ApplicationDetails(int id)
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        var result = await FindOwnedApplication(id, employerId.Value);
        if (result is null)
        {
            return NotFound();
        }

        ViewBag.Job = result.Value.Job;
        ViewBag.Applicant = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserID == result.Value.Application.JobSeekerID);
        ViewBag.Interviews = await _context.JobInterviews
            .AsNoTracking()
            .Where(interview => interview.ApplicationID == id)
            .OrderByDescending(interview => interview.InterviewDate)
            .ToListAsync();
        ViewBag.Statuses = ApplicationStatuses;

        return View(result.Value.Application);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        var normalizedStatus = ApplicationStatuses.FirstOrDefault(item =>
            string.Equals(item, status, StringComparison.OrdinalIgnoreCase));
        if (normalizedStatus is null)
        {
            TempData["Error"] = "Select a valid application status.";
            return RedirectToAction(nameof(ApplicationDetails), new { id });
        }

        var application = await (
            from item in _context.JobApplications
            join job in _context.JobVacancies on item.JobID equals job.JobID
            where item.ApplicationID == id && job.EmployerID == employerId.Value
            select item).FirstOrDefaultAsync();

        if (application is null)
        {
            return NotFound();
        }

        application.Status = normalizedStatus;
        await _context.SaveChangesAsync();
        TempData["Success"] = "Application status updated.";
        return RedirectToAction(nameof(ApplicationDetails), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> CreateInterview(int applicationId)
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        var result = await FindOwnedApplication(applicationId, employerId.Value);
        if (result is null)
        {
            return NotFound();
        }

        ViewBag.Application = result.Value.Application;
        ViewBag.Job = result.Value.Job;
        ViewBag.InterviewTypes = InterviewTypes;
        return View(new JobInterview
        {
            ApplicationID = applicationId,
            InterviewDate = DateTime.Now.AddDays(1),
            InterviewType = InterviewTypes[0],
            Location = string.Empty,
            Notes = string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInterview(
        [Bind("ApplicationID,InterviewDate,InterviewType,Location,Notes")] JobInterview input)
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        var result = await FindOwnedApplication(input.ApplicationID, employerId.Value);
        if (result is null)
        {
            return NotFound();
        }

        ModelState.Remove(nameof(JobInterview.Location));
        ModelState.Remove(nameof(JobInterview.Notes));
        ModelState.Remove(nameof(JobInterview.Status));
        input.Location = input.Location?.Trim() ?? string.Empty;
        input.Notes = input.Notes?.Trim() ?? string.Empty;

        var normalizedType = InterviewTypes.FirstOrDefault(type =>
            string.Equals(type, input.InterviewType, StringComparison.OrdinalIgnoreCase));
        if (normalizedType is null)
        {
            ModelState.AddModelError(nameof(input.InterviewType), "Select a valid interview type.");
        }
        else
        {
            input.InterviewType = normalizedType;
        }

        if (input.InterviewDate <= DateTime.Now)
        {
            ModelState.AddModelError(nameof(input.InterviewDate), "Interview date must be in the future.");
        }

        if (input.InterviewType == "On-site" && string.IsNullOrWhiteSpace(input.Location))
        {
            ModelState.AddModelError(nameof(input.Location), "Enter a location for an on-site interview.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Application = result.Value.Application;
            ViewBag.Job = result.Value.Job;
            ViewBag.InterviewTypes = InterviewTypes;
            return View(input);
        }

        input.Status = "Scheduled";
        _context.JobInterviews.Add(input);
        if (result.Value.Application.Status is "Submitted" or "Under Review")
        {
            result.Value.Application.Status = "Shortlisted";
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Interview scheduled successfully.";
        return RedirectToAction(nameof(ApplicationDetails), new { id = input.ApplicationID });
    }

    private async Task<(JobApplication Application, JobVacancy Job)?> FindOwnedApplication(
        int applicationId,
        int employerId)
    {
        var result = await (
            from application in _context.JobApplications
            join job in _context.JobVacancies on application.JobID equals job.JobID
            where application.ApplicationID == applicationId && job.EmployerID == employerId
            select new { Application = application, Job = job }).FirstOrDefaultAsync();

        return result is null ? null : (result.Application, result.Job);
    }
}

