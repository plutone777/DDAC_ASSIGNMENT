using DDAC.Data;
using DDAC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DDAC.Controllers.Employer;

public class EmployerProfileController : EmployerControllerBase
{
    private readonly ApplicationDbContext _context;

    public EmployerProfileController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        var profile = await _context.EmployerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.EmployerID == employerId.Value);

        if (profile is null)
        {
            TempData["Info"] = "Complete your company profile before posting vacancies.";
            return RedirectToAction(nameof(EditProfile));
        }

        ViewBag.User = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserID == employerId.Value);

        return View(profile);
    }

    [HttpGet]
    public async Task<IActionResult> EditProfile()
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        var profile = await _context.EmployerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.EmployerID == employerId.Value)
            ?? new EmployerProfile
            {
                EmployerID = employerId.Value,
                CompanyName = string.Empty,
                Industry = string.Empty,
                CompanyDescription = string.Empty,
                Address = string.Empty,
                Website = string.Empty
            };

        return View(profile);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(
        [Bind("CompanyName,Industry,CompanyDescription,Address,Website")] EmployerProfile input)
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        RemoveOptionalValidationEntries();
        input.CompanyName = input.CompanyName?.Trim() ?? string.Empty;
        input.Industry = input.Industry?.Trim() ?? string.Empty;
        input.CompanyDescription = input.CompanyDescription?.Trim() ?? string.Empty;
        input.Address = input.Address?.Trim() ?? string.Empty;
        input.Website = input.Website?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(input.Website) &&
            (!Uri.TryCreate(input.Website, UriKind.Absolute, out var website) ||
             (website.Scheme != Uri.UriSchemeHttp && website.Scheme != Uri.UriSchemeHttps)))
        {
            ModelState.AddModelError(nameof(input.Website), "Enter a complete website URL beginning with http:// or https://.");
        }

        if (!ModelState.IsValid)
        {
            input.EmployerID = employerId.Value;
            return View(input);
        }

        var existing = await _context.EmployerProfiles
            .FirstOrDefaultAsync(item => item.EmployerID == employerId.Value);

        if (existing is null)
        {
            input.EmployerID = employerId.Value;
            input.VerificationStatus = "Pending";
            _context.EmployerProfiles.Add(input);
        }
        else
        {
            existing.CompanyName = input.CompanyName;
            existing.Industry = input.Industry;
            existing.CompanyDescription = input.CompanyDescription;
            existing.Address = input.Address;
            existing.Website = input.Website;
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Company profile saved successfully.";
        return RedirectToAction(nameof(Profile));
    }

    private void RemoveOptionalValidationEntries()
    {
        ModelState.Remove(nameof(EmployerProfile.User));
        ModelState.Remove(nameof(EmployerProfile.CompanyDescription));
        ModelState.Remove(nameof(EmployerProfile.Address));
        ModelState.Remove(nameof(EmployerProfile.Website));
        ModelState.Remove(nameof(EmployerProfile.VerificationStatus));
    }
}

