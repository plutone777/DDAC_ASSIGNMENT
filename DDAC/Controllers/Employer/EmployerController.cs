using DDAC.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DDAC.Controllers.Employer;

public abstract class EmployerControllerBase : Controller
{
    protected const string EmployerViewRoot = "~/Views/Employer/";
    protected const string EmployerRole = "Employer";
    protected const string ActiveStatus = "Active";
    protected const string PublishedStatus = "Published";
    protected const string DraftStatus = "Draft";

    protected static readonly IReadOnlyList<string> VacancyStatuses =
        [DraftStatus, PublishedStatus, "Closed"];

    protected static readonly IReadOnlyList<string> ApplicationStatuses =
        ["Submitted", "Under Review", "Shortlisted", "Rejected", "Hired"];

    protected int? CurrentEmployerId
    {
        get
        {
            var role = HttpContext.Session.GetString("Role");
            return string.Equals(role, EmployerRole, StringComparison.OrdinalIgnoreCase)
                ? HttpContext.Session.GetInt32("UserID")
                : null;
        }
    }

    protected IActionResult RedirectToLogin()
    {
        TempData["Error"] = "Please sign in with an employer account to continue.";
        return RedirectToAction("Login", "User");
    }
}

public class EmployerController : EmployerControllerBase
{
    private readonly ApplicationDbContext _context;

    public EmployerController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        var profile = await _context.EmployerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.EmployerID == employerId.Value);

        var vacancies = await _context.JobVacancies
            .AsNoTracking()
            .Where(vacancy => vacancy.EmployerID == employerId.Value)
            .OrderByDescending(vacancy => vacancy.PostedDate)
            .ToListAsync();

        var vacancyIds = vacancies.Select(vacancy => vacancy.JobID).ToList();
        var applications = await _context.JobApplications
            .AsNoTracking()
            .Where(application => vacancyIds.Contains(application.JobID))
            .OrderByDescending(application => application.ApplicationDate)
            .ToListAsync();

        var applicationIds = applications.Select(application => application.ApplicationID).ToList();
        var upcomingInterviewCount = await _context.JobInterviews
            .AsNoTracking()
            .CountAsync(interview =>
                applicationIds.Contains(interview.ApplicationID) &&
                interview.InterviewDate >= DateTime.Now &&
                interview.Status == "Scheduled");

        var applicantIds = applications
            .Take(5)
            .Select(application => application.JobSeekerID)
            .Distinct()
            .ToList();

        ViewBag.Profile = profile;
        ViewBag.VacancyCount = vacancies.Count;
        ViewBag.PublishedVacancyCount = vacancies.Count(vacancy => vacancy.Status == PublishedStatus);
        ViewBag.ApplicationCount = applications.Count;
        ViewBag.UpcomingInterviewCount = upcomingInterviewCount;
        ViewBag.RecentApplications = applications.Take(5).ToList();
        ViewBag.JobsById = vacancies.ToDictionary(vacancy => vacancy.JobID);
        ViewBag.ApplicantsById = await _context.Users
            .AsNoTracking()
            .Where(user => applicantIds.Contains(user.UserID))
            .ToDictionaryAsync(user => user.UserID);

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> HiringGuidance()
    {
        if (CurrentEmployerId is null)
        {
            return RedirectToLogin();
        }

        ViewBag.Announcements = await _context.Announcements
            .AsNoTracking()
            .Where(announcement => announcement.Status == PublishedStatus)
            .OrderByDescending(announcement => announcement.PublishedDate)
            .ToListAsync();

        var resources = await _context.CareerResources
            .AsNoTracking()
            .Where(resource => resource.Status == PublishedStatus)
            .OrderByDescending(resource => resource.PublishedDate)
            .ToListAsync();

        return View(resources);
    }
}
