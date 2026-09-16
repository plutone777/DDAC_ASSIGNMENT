using DDAC.Data;
using DDAC.Contracts.Employer;
using DDAC.Models;
using DDAC.Services.Employer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DDAC.Controllers.Employer;

public class EmployerApplicationController : EmployerControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IInterviewSchedulingService _interviewScheduling;
    private readonly IInterviewApiClient _interviewApi;

    public EmployerApplicationController(ApplicationDbContext context, IInterviewSchedulingService interviewScheduling, IInterviewApiClient interviewApi)
    {
        _context = context;
        _interviewScheduling = interviewScheduling;
        _interviewApi = interviewApi;
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

        return View(EmployerViewRoot + "Applications.cshtml", applications);
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

        return View(EmployerViewRoot + "ApplicationDetails.cshtml", result.Value.Application);
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
        ViewBag.InterviewTypes = _interviewScheduling.InterviewTypes;
        return View(EmployerViewRoot + "CreateInterview.cshtml", new JobInterview
        {
            ApplicationID = applicationId,
            InterviewDate = DateTime.Now.AddDays(1),
            InterviewType = _interviewScheduling.InterviewTypes[0],
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

        ModelState.Remove(nameof(JobInterview.Location));
        ModelState.Remove(nameof(JobInterview.Notes));
        ModelState.Remove(nameof(JobInterview.Status));
        var owned = await FindOwnedApplication(input.ApplicationID, employerId.Value);
        if (owned is null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var result = await _interviewApi.ScheduleAsync(employerId.Value, new ScheduleInterviewRequest
            {
                ApplicationID = input.ApplicationID, InterviewDate = input.InterviewDate,
                InterviewType = input.InterviewType, Location = input.Location, Notes = input.Notes
            });
            if (result.Response?.Success == true)
            {
                TempData["Success"] = "Interview scheduled successfully.";
                return RedirectToAction(nameof(ApplicationDetails), new { id = input.ApplicationID });
            }
            if (result.Response?.ErrorCode == ScheduleInterviewResponse.NotFound) return NotFound();
            if (result.Response?.ErrorCode == ScheduleInterviewResponse.ValidationFailed)
            {
                foreach (var error in result.Response.Errors)
                {
                    var field = error.Key switch
                    {
                        "applicationID" => nameof(input.ApplicationID), "interviewDate" => nameof(input.InterviewDate),
                        "interviewType" => nameof(input.InterviewType), "location" => nameof(input.Location),
                        "notes" => nameof(input.Notes), _ => ""
                    };
                    foreach (var message in error.Value) ModelState.AddModelError(field, message);
                }
            }
            else ModelState.AddModelError("", result.Unavailable
                ? "Interview scheduling is temporarily unavailable. Check application details before submitting again."
                : "Unable to confirm interview scheduling. Check application details before submitting again.");
        }
        ViewBag.Application = owned.Value.Application;
        ViewBag.Job = owned.Value.Job;
        ViewBag.InterviewTypes = _interviewScheduling.InterviewTypes;
        return View(EmployerViewRoot + "CreateInterview.cshtml", input);
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
