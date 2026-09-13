using Microsoft.AspNetCore.Mvc;
using DDAC.Data;
using Microsoft.EntityFrameworkCore;
using DDAC.Models;
namespace DDAC.Controllers.JobSeeker
{
    public class JobSeekerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public JobSeekerController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var fullName = HttpContext.Session.GetString("FullName");

            ViewBag.FullName = fullName;

            return View();
        }

        [HttpGet]
        public IActionResult EditProfile()
        {
            return View();
        }

        [HttpGet]
        public IActionResult BrowseJobs(
            string? search,
            string? employmentType,
            string? accessibility,
            string? accommodation)
        {
            var jobsQuery = _context.JobVacancies
                .Where(j => j.Status == "Published")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                jobsQuery = jobsQuery.Where(j =>
                    j.JobTitle.Contains(search) ||
                    j.Description.Contains(search) ||
                    j.Location.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(employmentType))
            {
                jobsQuery = jobsQuery.Where(j =>
                    j.EmploymentType == employmentType);
            }

            if (!string.IsNullOrWhiteSpace(accessibility))
            {
                jobsQuery = jobsQuery.Where(j =>
                    j.AccessibilityFeatures != null &&
                    j.AccessibilityFeatures.Contains(accessibility));
            }

            if (!string.IsNullOrWhiteSpace(accommodation))
            {
                jobsQuery = jobsQuery.Where(j =>
                    j.AccommodationsAvailable != null &&
                    j.AccommodationsAvailable.Contains(accommodation));
            }

            var jobs = jobsQuery
                .OrderByDescending(j => j.PostedDate)
                .ToList();


            var allJobs = _context.JobVacancies
                .Where(j => j.Status == "Published")
                .ToList();

            var employmentTypes = allJobs
                .Select(j => j.EmploymentType)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var accessibilityFeatures = allJobs
                .SelectMany(j => (j.AccessibilityFeatures ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var accommodations = allJobs
                .SelectMany(j => (j.AccommodationsAvailable ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            ViewBag.EmploymentTypes = employmentTypes;
            ViewBag.AccessibilityFeatures = accessibilityFeatures;
            ViewBag.Accommodations = accommodations;

            ViewBag.Search = search;
            ViewBag.SelectedEmploymentType = employmentType;
            ViewBag.SelectedAccessibility = accessibility;
            ViewBag.SelectedAccommodation = accommodation;

            return View(jobs);
        }

        [HttpGet]
        public IActionResult JobDetails(int id)
        {
            var job = _context.JobVacancies
                .FirstOrDefault(j => j.JobID == id);

            if (job == null)
            {
                return NotFound();
            }

            var employer = _context.EmployerProfiles
                .FirstOrDefault(e => e.EmployerID == job.EmployerID);

            ViewBag.Employer = employer;

            return View(job);
        }

        [HttpGet]
        public IActionResult MyApplications()
        {
            var userID = HttpContext.Session.GetInt32("UserID");

            if (userID == null)
            {
                return RedirectToAction("Login", "User");
            }

            var applications = _context.JobApplications
                .Where(a => a.JobSeekerID == userID)
                .OrderByDescending(a => a.ApplicationDate)
                .ToList();

            var jobs = _context.JobVacancies.ToList();

            ViewBag.Jobs = jobs;

            return View(applications);
        }
        [HttpGet]
        public IActionResult Information()
        {
            var resources = _context.CareerResources
                .Where(r => r.Status == "Published")
                .OrderByDescending(r => r.PublishedDate)
                .ToList();

            return View(resources);
        }

        [HttpGet]
        public async Task<IActionResult> Announcements()
        {
            var announcements = await _context.Announcements
                .Where(a => a.Status == "Published")
                .OrderByDescending(a => a.PublishedDate)
                .ToListAsync();

            return View(
                "~/Views/JobSeeker/Announcements.cshtml",
                announcements
            );
        }
    }
}