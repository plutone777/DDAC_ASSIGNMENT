using DDAC.Data;
using DDAC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DDAC.Controllers
{
    public class InquiryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InquiryController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Submit()
        {
            var userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null)
            {
                return RedirectToAction("Login", "User");
            }

            await LoadAdvisors();

            return View(
                "~/Views/JobSeeker/SubmitInquiry.cshtml",
                new Inquiry()
            );
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(Inquiry inquiry)
        {
            var userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null)
            {
                return RedirectToAction("Login", "User");
            }

            inquiry.UserID = userId.Value;
            inquiry.Status = "Open";
            inquiry.CreatedDate = DateTime.Now;

            if (!ModelState.IsValid)
            {
                await LoadAdvisors();

                return View(
                    "~/Views/JobSeeker/SubmitInquiry.cshtml",
                    inquiry
                );
            }

            _context.Inquiries.Add(inquiry);

            await _context.SaveChangesAsync();

            TempData["InquirySuccess"] =
                "Your inquiry has been submitted successfully.";

            return RedirectToAction(
                "MyInquiries",
                "Inquiry"
            );
        }

        [HttpGet]
        public async Task<IActionResult> MyInquiries(string? status)
        {
            var userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null)
            {
                return RedirectToAction("Login", "User");
            }

            var query = _context.Inquiries
                .Where(i => i.UserID == userId.Value);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(i => i.Status == status);
            }

            var inquiries = await query
                .OrderByDescending(i => i.CreatedDate)
                .ToListAsync();

            // Load advisor names
            var advisors = await _context.CareerAdvisorProfiles
                .Include(a => a.User)
                .ToListAsync();

            ViewBag.SelectedStatus = status;
            ViewBag.Advisors = advisors;

            return View(
                "~/Views/JobSeeker/MyInquiries.cshtml",
                inquiries
            );
        }

        [HttpGet]
        public async Task<IActionResult> RequestGuidance()
        {
            var userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null)
            {
                return RedirectToAction("Login", "User");
            }

            await LoadAdvisors();

            ViewBag.Recommendations =
                await _context.CareerRecommendations
                    .Where(r => r.JobSeekerID == userId.Value)
                    .OrderByDescending(r => r.DateCreated)
                    .ToListAsync();

            return View(
                "~/Views/JobSeeker/CareerGuidance.cshtml",
                new CareerGuidance
                {
                    JobSeekerID = userId.Value,
                    GuidanceDate = DateTime.Now,
                    Status = "Requested"
                }
            );
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestGuidance(
            CareerGuidance guidance)
        {
            var userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null)
            {
                return RedirectToAction("Login", "User");
            }

            guidance.JobSeekerID = userId.Value;
            guidance.GuidanceDate = DateTime.Now;
            guidance.Status = "Requested";

            if (!ModelState.IsValid)
            {
                await LoadAdvisors();

                ViewBag.Recommendations =
                    await _context.CareerRecommendations
                        .Where(r => r.JobSeekerID == userId.Value)
                        .OrderByDescending(r => r.DateCreated)
                        .ToListAsync();

                return View(
                    "~/Views/JobSeeker/CareerGuidance.cshtml",
                    guidance
                );
            }

            _context.CareerGuidances.Add(guidance);

            await _context.SaveChangesAsync();

            TempData["GuidanceSuccess"] =
                "Your career guidance request has been submitted successfully.";

            return RedirectToAction(
                "RequestGuidance",
                "Inquiry"
            );
        }

        [HttpGet]
        public async Task<IActionResult> MyRequests(string? status)
        {
            var userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null)
            {
                return RedirectToAction("Login", "User");
            }

            var query = _context.CareerGuidances
                .Where(g => g.JobSeekerID == userId.Value);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(g => g.Status == status);
            }

            var requests = await query
                .OrderByDescending(g => g.GuidanceDate)
                .ToListAsync();

            // Load advisor names
            var advisors = await _context.CareerAdvisorProfiles
                .Include(a => a.User)
                .ToListAsync();

            ViewBag.SelectedStatus = status;
            ViewBag.Advisors = advisors;

            return View(
                "~/Views/JobSeeker/MyRequests.cshtml",
                requests
            );
        }

        [HttpGet]
        public async Task<IActionResult> CareerSupport()
        {
            var userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null)
            {
                return RedirectToAction("Login", "User");
            }

            await LoadAdvisors();

            ViewBag.Recommendations =
                await _context.CareerRecommendations
                    .Where(r => r.JobSeekerID == userId.Value)
                    .OrderByDescending(r => r.DateCreated)
                    .ToListAsync();

            return View(
                "~/Views/JobSeeker/CareerSupport.cshtml"
            );
        }

        [HttpGet]
        public async Task<IActionResult> MyRecommendations()
        {
            var userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null)
            {
                return RedirectToAction("Login", "User");
            }

            var recommendations =
                await _context.CareerRecommendations
                    .Where(r => r.JobSeekerID == userId.Value)
                    .OrderByDescending(r => r.DateCreated)
                    .ToListAsync();

            var advisors = await _context.CareerAdvisorProfiles
                .Include(a => a.User)
                .ToListAsync();

            ViewBag.Advisors = advisors;

            return View(
                "~/Views/JobSeeker/GetRecommendations.cshtml",
                recommendations
            );
        }

        private async Task LoadAdvisors()
        {
            var advisors = await _context.CareerAdvisorProfiles
                .Include(a => a.User)
                .Where(a => a.User != null &&
                            a.User.Status == "Active")
                .ToListAsync();

            ViewBag.Advisors = advisors
                .Select(a => new SelectListItem
                {
                    Value = a.AdvisorID.ToString(),
                    Text = a.User!.FullName
                })
                .ToList();
        }
    }
}