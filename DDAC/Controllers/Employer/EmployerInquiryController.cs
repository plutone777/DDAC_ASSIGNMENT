using DDAC.Data;
using DDAC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DDAC.Controllers.Employer;

public class EmployerInquiryController : EmployerControllerBase
{
    private readonly ApplicationDbContext _context;

    public EmployerInquiryController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Inquiries()
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        var inquiries = await _context.Inquiries
            .AsNoTracking()
            .Where(inquiry => inquiry.UserID == employerId.Value)
            .OrderByDescending(inquiry => inquiry.CreatedDate)
            .ToListAsync();

        var advisorIds = inquiries.Select(inquiry => inquiry.AdvisorID).Distinct().ToList();
        ViewBag.AdvisorsById = await _context.Users
            .AsNoTracking()
            .Where(user => advisorIds.Contains(user.UserID))
            .ToDictionaryAsync(user => user.UserID);
        return View(inquiries);
    }

    [HttpGet]
    public async Task<IActionResult> CreateInquiry()
    {
        if (CurrentEmployerId is null)
        {
            return RedirectToLogin();
        }

        await PopulateAdvisorOptions();
        return View(new Inquiry
        {
            Subject = string.Empty,
            Message = string.Empty,
            Status = "Open",
            CreatedDate = DateTime.Now
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInquiry(
        [Bind("AdvisorID,Subject,Message")] Inquiry input)
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        ModelState.Remove(nameof(Inquiry.Response));
        ModelState.Remove(nameof(Inquiry.Status));
        ModelState.Remove(nameof(Inquiry.CreatedDate));
        input.Subject = input.Subject?.Trim() ?? string.Empty;
        input.Message = input.Message?.Trim() ?? string.Empty;

        var advisorExists = await (
            from profile in _context.CareerAdvisorProfiles
            join user in _context.Users on profile.AdvisorID equals user.UserID
            where profile.AdvisorID == input.AdvisorID &&
                  user.Role == "CareerAdvisor" &&
                  user.Status == ActiveStatus
            select profile.AdvisorID).AnyAsync();

        if (!advisorExists)
        {
            ModelState.AddModelError(nameof(input.AdvisorID), "Select an active career advisor.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAdvisorOptions(input.AdvisorID);
            return View(input);
        }

        input.UserID = employerId.Value;
        input.Status = "Open";
        input.CreatedDate = DateTime.Now;
        input.Response = null;
        input.ResolvedDate = null;
        _context.Inquiries.Add(input);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Inquiry sent to the career advisor.";
        return RedirectToAction(nameof(InquiryDetails), new { id = input.InquiryID });
    }

    [HttpGet]
    public async Task<IActionResult> InquiryDetails(int id)
    {
        var employerId = CurrentEmployerId;
        if (employerId is null)
        {
            return RedirectToLogin();
        }

        var inquiry = await _context.Inquiries
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.InquiryID == id && item.UserID == employerId.Value);
        if (inquiry is null)
        {
            return NotFound();
        }

        ViewBag.Advisor = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserID == inquiry.AdvisorID);
        return View(inquiry);
    }

    private async Task PopulateAdvisorOptions(int? selectedAdvisorId = null)
    {
        var advisors = await (
            from profile in _context.CareerAdvisorProfiles.AsNoTracking()
            join user in _context.Users.AsNoTracking() on profile.AdvisorID equals user.UserID
            where user.Role == "CareerAdvisor" && user.Status == ActiveStatus
            orderby user.FullName
            select new
            {
                Id = profile.AdvisorID,
                Label = user.FullName + " — " + profile.Specialisation
            }).ToListAsync();

        ViewBag.AdvisorOptions = new SelectList(advisors, "Id", "Label", selectedAdvisorId);
    }
}

