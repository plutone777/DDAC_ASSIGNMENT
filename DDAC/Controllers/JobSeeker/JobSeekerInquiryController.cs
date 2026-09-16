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
        private readonly HttpClient _httpClient;
        private readonly string _inquiryApiUrl;
        private readonly string _guidanceApiUrl;

        public InquiryController(
            ApplicationDbContext context,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _context = context;
            _httpClient = httpClient;

            _inquiryApiUrl =
                configuration["ApiSettings:InquiryApiUrl"]
                ?? throw new InvalidOperationException(
                    "Inquiry API URL is not configured.");

            _guidanceApiUrl =
                configuration["ApiSettings:GuidanceApiUrl"]
                ?? throw new InvalidOperationException(
                    "Guidance API URL is not configured.");
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
                "~/Views/JobSeeker/CareerSupport.cshtml",
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

            if (!ModelState.IsValid)
            {
                await LoadAdvisors();

                return View(
                    "~/Views/JobSeeker/CareerSupport.cshtml",
                    inquiry
                );
            }

            Console.WriteLine("INQUIRY POST START");

            var response = await _httpClient.PostAsJsonAsync(
                _inquiryApiUrl,
                inquiry);

            Console.WriteLine("INQUIRY API CALL COMPLETED");

            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine(
                $"Inquiry API Status: {(int)response.StatusCode} {response.StatusCode}");

            Console.WriteLine(
                $"Inquiry API Response: {responseBody}");

            if (!response.IsSuccessStatusCode)
            {
                TempData["InquiryError"] =
                    "Unable to submit your inquiry. Please try again.";

                await LoadAdvisors();

                return View(
                    "~/Views/JobSeeker/CareerSupport.cshtml",
                    inquiry
                );
            }

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
                "~/Views/JobSeeker/MyRequests.cshtml",
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

            if (!ModelState.IsValid)
            {
                await LoadAdvisors();

                ViewBag.Recommendations =
                    await _context.CareerRecommendations
                        .Where(r => r.JobSeekerID == userId.Value)
                        .OrderByDescending(r => r.DateCreated)
                        .ToListAsync();

                return View(
                    "~/Views/JobSeeker/CareerSupport.cshtml",
                    guidance
                );
            }

            var response = await _httpClient.PostAsJsonAsync(
                _guidanceApiUrl,
                guidance);

            if (!response.IsSuccessStatusCode)
            {
                TempData["GuidanceError"] =
                    "Unable to submit your career guidance request. Please try again.";

                await LoadAdvisors();

                ViewBag.Recommendations =
                    await _context.CareerRecommendations
                        .Where(r => r.JobSeekerID == userId.Value)
                        .OrderByDescending(r => r.DateCreated)
                        .ToListAsync();

                return View(
                    "~/Views/JobSeeker/CareerSupport.cshtml",
                    guidance
                );
            }

            TempData["GuidanceSuccess"] =
                "Your career guidance request has been submitted successfully.";

            return RedirectToAction(
                "MyRequests",
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

            ViewBag.Advisors = advisors;
        }
    }
}