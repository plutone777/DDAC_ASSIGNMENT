using DDAC.Data;
using DDAC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DDAC.Controllers.Employer;

public class JobVacancyController : EmployerControllerBase
{
    private readonly ApplicationDbContext _context;

    public JobVacancyController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Vacancies(string? status)
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        var query = _context.JobVacancies
            .AsNoTracking()
            .Where(vacancy => vacancy.EmployerID == employerId.Value);

        if (!string.IsNullOrWhiteSpace(status) && VacancyStatuses.Contains(status))
        {
            query = query.Where(vacancy => vacancy.Status == status);
        }

        ViewBag.SelectedStatus = status;
        ViewBag.Statuses = VacancyStatuses;
        return View(await query.OrderByDescending(vacancy => vacancy.PostedDate).ToListAsync());
    }

    [HttpGet]
    public IActionResult CreateVacancy()
    {
        if (CurrentEmployerId is null)
        {
            return RedirectToLogin();
        }

        ViewBag.Statuses = VacancyStatuses;
        return View(new JobVacancy
        {
            JobTitle = string.Empty,
            Description = string.Empty,
            Location = string.Empty,
            EmploymentType = string.Empty,
            AccessibilityFeatures = string.Empty,
            AccommodationsAvailable = string.Empty,
            ClosingDate = DateTime.Today.AddDays(30),
            Status = DraftStatus
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVacancy(
        [Bind("JobTitle,Description,Location,EmploymentType,Salary,AccessibilityFeatures,AccommodationsAvailable,ClosingDate,Status")] JobVacancy input)
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        NormalizeAndValidate(input);
        if (!ModelState.IsValid)
        {
            ViewBag.Statuses = VacancyStatuses;
            return View(input);
        }

        input.EmployerID = employerId.Value;
        input.PostedDate = DateTime.Now;
        _context.JobVacancies.Add(input);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Vacancy created successfully.";
        return RedirectToAction(nameof(VacancyDetails), new { id = input.JobID });
    }

    [HttpGet]
    public async Task<IActionResult> EditVacancy(int id)
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        var vacancy = await FindOwnedVacancy(id, employerId.Value, tracking: false);
        if (vacancy is null)
        {
            return NotFound();
        }

        ViewBag.Statuses = VacancyStatuses;
        return View(vacancy);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditVacancy(
        int id,
        [Bind("JobTitle,Description,Location,EmploymentType,Salary,AccessibilityFeatures,AccommodationsAvailable,ClosingDate,Status")] JobVacancy input)
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        var existing = await FindOwnedVacancy(id, employerId.Value, tracking: true);
        if (existing is null)
        {
            return NotFound();
        }

        NormalizeAndValidate(input);
        if (!ModelState.IsValid)
        {
            input.JobID = id;
            input.EmployerID = employerId.Value;
            input.PostedDate = existing.PostedDate;
            ViewBag.Statuses = VacancyStatuses;
            return View(input);
        }

        existing.JobTitle = input.JobTitle;
        existing.Description = input.Description;
        existing.Location = input.Location;
        existing.EmploymentType = input.EmploymentType;
        existing.Salary = input.Salary;
        existing.AccessibilityFeatures = input.AccessibilityFeatures;
        existing.AccommodationsAvailable = input.AccommodationsAvailable;
        existing.ClosingDate = input.ClosingDate;
        existing.Status = input.Status;

        await _context.SaveChangesAsync();
        TempData["Success"] = "Vacancy updated successfully.";
        return RedirectToAction(nameof(VacancyDetails), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> VacancyDetails(int id)
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        var vacancy = await FindOwnedVacancy(id, employerId.Value, tracking: false);
        if (vacancy is null)
        {
            return NotFound();
        }

        ViewBag.ApplicationCount = await _context.JobApplications
            .CountAsync(application => application.JobID == vacancy.JobID);
        return View(vacancy);
    }

    private async Task<JobVacancy?> FindOwnedVacancy(int id, int employerId, bool tracking)
    {
        var query = _context.JobVacancies
            .Where(vacancy => vacancy.JobID == id && vacancy.EmployerID == employerId);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync();
    }

    private void NormalizeAndValidate(JobVacancy input)
    {
        ModelState.Remove(nameof(JobVacancy.EmployerID));
        ModelState.Remove(nameof(JobVacancy.PostedDate));
        ModelState.Remove(nameof(JobVacancy.Location));
        ModelState.Remove(nameof(JobVacancy.AccessibilityFeatures));
        ModelState.Remove(nameof(JobVacancy.AccommodationsAvailable));

        input.JobTitle = input.JobTitle?.Trim() ?? string.Empty;
        input.Description = input.Description?.Trim() ?? string.Empty;
        input.Location = input.Location?.Trim() ?? string.Empty;
        input.EmploymentType = input.EmploymentType?.Trim() ?? string.Empty;
        input.AccessibilityFeatures = input.AccessibilityFeatures?.Trim() ?? string.Empty;
        input.AccommodationsAvailable = input.AccommodationsAvailable?.Trim() ?? string.Empty;

        if (input.Salary < 0)
        {
            ModelState.AddModelError(nameof(input.Salary), "Salary cannot be negative.");
        }

        if (input.ClosingDate.Date < DateTime.Today)
        {
            ModelState.AddModelError(nameof(input.ClosingDate), "Closing date cannot be in the past.");
        }

        var validStatus = VacancyStatuses.FirstOrDefault(status =>
            string.Equals(status, input.Status, StringComparison.OrdinalIgnoreCase));
        if (validStatus is null)
        {
            ModelState.AddModelError(nameof(input.Status), "Select a valid vacancy status.");
        }
        else
        {
            input.Status = validStatus;
        }
    }
}

