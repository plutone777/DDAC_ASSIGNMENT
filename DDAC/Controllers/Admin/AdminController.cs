using DDAC.Data;
using DDAC.Models;
using DDAC.Models.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DDAC.Controllers.Admin
{
    public class AdminController : Controller, IAsyncActionFilter
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var role = HttpContext.Session.GetString("Role");

            if (role != "Admin")
            {
                context.Result = RedirectToAction("Login", "User");
                return;
            }

            var accessibilitySettings = await _context.SystemSettings
                .Where(s => s.SettingCategory == "Accessibility")
                .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

            ViewBag.FontSize = accessibilitySettings.GetValueOrDefault("DefaultFontSize", "Medium");
            ViewBag.HighContrast = accessibilitySettings.GetValueOrDefault("HighContrastDefault", "false")
                .Equals("true", StringComparison.OrdinalIgnoreCase);
            ViewBag.ReducedMotion = accessibilitySettings.GetValueOrDefault("ReducedMotionDefault", "false")
                .Equals("true", StringComparison.OrdinalIgnoreCase);
            ViewBag.ScreenReaderHints = accessibilitySettings.GetValueOrDefault("ScreenReaderHints", "false")
                .Equals("true", StringComparison.OrdinalIgnoreCase);

            await next();
        }

        // ---------- Entry point after login ----------
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.ActiveUsers = await _context.Users.CountAsync(u => u.Status == "Active");
            ViewBag.PendingEmployerCount = await _context.EmployerProfiles.CountAsync(e => e.VerificationStatus == "Pending");
            ViewBag.OpenJobPostings = await _context.JobVacancies.CountAsync(j => j.Status == "Open");
            ViewBag.PublishedResources = await _context.CareerResources.CountAsync(r => r.Status == "Published");
            ViewBag.PublishedAnnouncements = await _context.Announcements.CountAsync(a => a.Status == "Published");

            ViewBag.NeedsReview = await _context.EmployerProfiles
                .Where(e => e.VerificationStatus == "Pending")
                .Take(5)
                .ToListAsync();

            return View();
        }

        // ===================== Function 1 =====================

        public async Task<IActionResult> UserAccounts()
        {
            var users = await _context.Users
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var jobSeekerCounts = await _context.JobApplications
                .GroupBy(a => a.JobSeekerID)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var employerCounts = await _context.JobVacancies
                .GroupBy(j => j.EmployerID)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var advisorCounts = await _context.CareerResources
                .GroupBy(r => r.AdvisorID)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var adminCounts = await _context.Announcements
                .GroupBy(a => a.AdminID)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var rows = users.Select(u => new UserAccountRow
            {
                User = u,
                PostCount = u.Role switch
                {
                    "JobSeeker" => jobSeekerCounts.GetValueOrDefault(u.UserID),
                    "Employer" => employerCounts.GetValueOrDefault(u.UserID),
                    "CareerAdvisor" => advisorCounts.GetValueOrDefault(u.UserID),
                    "Admin" => adminCounts.GetValueOrDefault(u.UserID),
                    _ => 0
                }
            }).ToList();

            return View(rows);
        }

        public async Task<IActionResult> UserDetail(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var vm = new UserDetailViewModel { User = user };

            switch (user.Role)
            {
                case "JobSeeker":
                    vm.JobSeekerProfile = await _context.JobSeekerProfiles.FindAsync(id);

                    vm.Skills = await (
                        from js in _context.JobSeekerSkills
                        join s in _context.Skills on js.SkillID equals s.SkillID
                        where js.JobSeekerID == id
                        select s.SkillName + " (" + js.SkillLevel + ")"
                    ).ToListAsync();

                    vm.Qualifications = await _context.Qualifications
                        .Where(q => q.JobSeekerID == id)
                        .ToListAsync();

                    vm.Applications = await (
                        from a in _context.JobApplications
                        join j in _context.JobVacancies on a.JobID equals j.JobID
                        where a.JobSeekerID == id
                        orderby a.ApplicationDate descending
                        select new ApplicationSummary
                        {
                            JobTitle = j.JobTitle,
                            ApplicationDate = a.ApplicationDate,
                            Status = a.Status,
                            ResumeURL = a.ResumeURL,
                            CoverLetter = a.CoverLetter
                        }
                    ).ToListAsync();
                    break;

                case "Employer":
                    vm.EmployerProfile = await _context.EmployerProfiles.FindAsync(id);
                    vm.JobPostings = await _context.JobVacancies
                        .Where(j => j.EmployerID == id)
                        .OrderByDescending(j => j.PostedDate)
                        .ToListAsync();
                    break;

                case "CareerAdvisor":
                    vm.CareerAdvisorProfile = await _context.CareerAdvisorProfiles.FindAsync(id);
                    vm.PublishedResources = await _context.CareerResources
                        .Where(r => r.AdvisorID == id)
                        .OrderByDescending(r => r.PublishedDate)
                        .ToListAsync();
                    break;

                case "Admin":
                    vm.Announcements = await _context.Announcements
                        .Where(a => a.AdminID == id)
                        .OrderByDescending(a => a.PublishedDate)
                        .ToListAsync();
                    break;
            }

            return View(vm);
        }

        // Downloadable report for one user
        public async Task<IActionResult> GenerateUserReport(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var sb = new StringBuilder();
            sb.AppendLine("USER REPORT");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("d MMM yyyy, HH:mm"));
            sb.AppendLine(new string('=', 60));
            sb.AppendLine();
            sb.AppendLine("BASIC INFO");
            sb.AppendLine("User ID: " + user.UserID);
            sb.AppendLine("Name: " + user.FullName);
            sb.AppendLine("Email: " + user.Email);
            sb.AppendLine("Phone: " + (string.IsNullOrEmpty(user.PhoneNumber) ? "-" : user.PhoneNumber));
            sb.AppendLine("Role: " + user.Role);
            sb.AppendLine("Status: " + user.Status);
            sb.AppendLine("Joined: " + user.CreatedDate.ToString("d MMM yyyy"));
            sb.AppendLine();

            switch (user.Role)
            {
                case "JobSeeker":
                    var jsProfile = await _context.JobSeekerProfiles.FindAsync(id);
                    sb.AppendLine("JOB SEEKER PROFILE");
                    sb.AppendLine("Career Goal: " + (jsProfile?.CareerGoal ?? "-"));
                    sb.AppendLine("Bio: " + (jsProfile?.Bio ?? "-"));
                    sb.AppendLine("Preferred Location: " + (jsProfile?.PreferredLocation ?? "-"));
                    sb.AppendLine("Resume URL: " + (jsProfile?.ResumeURL ?? "-"));
                    sb.AppendLine("Accommodation Needs: " + (jsProfile?.AccommodationNeeds ?? "-"));
                    sb.AppendLine();

                    var skills = await (
                        from js in _context.JobSeekerSkills
                        join s in _context.Skills on js.SkillID equals s.SkillID
                        where js.JobSeekerID == id
                        select s.SkillName + " (" + js.SkillLevel + ")"
                    ).ToListAsync();
                    sb.AppendLine("SKILLS (" + skills.Count + ")");
                    foreach (var sk in skills) sb.AppendLine("  - " + sk);
                    sb.AppendLine();

                    var quals = await _context.Qualifications.Where(q => q.JobSeekerID == id).ToListAsync();
                    sb.AppendLine("QUALIFICATIONS (" + quals.Count + ")");
                    foreach (var q in quals) sb.AppendLine("  - " + q.QualificationName + ", " + q.Institution + " (" + q.CompletionYear + ")");
                    sb.AppendLine();

                    var apps = await (
                        from a in _context.JobApplications
                        join j in _context.JobVacancies on a.JobID equals j.JobID
                        where a.JobSeekerID == id
                        orderby a.ApplicationDate descending
                        select new { j.JobTitle, a.ApplicationDate, a.Status, a.ResumeURL, a.CoverLetter }
                    ).ToListAsync();
                    sb.AppendLine("JOB APPLICATIONS (" + apps.Count + ")");
                    foreach (var a in apps)
                    {
                        sb.AppendLine("  - " + a.JobTitle + " | Applied " + a.ApplicationDate.ToString("d MMM yyyy") + " | " + a.Status);
                        sb.AppendLine("    Resume: " + (a.ResumeURL ?? "-"));
                        sb.AppendLine("    Cover Letter: " + (a.CoverLetter ?? "-"));
                    }
                    sb.AppendLine();
                    break;

                case "Employer":
                    var empProfile = await _context.EmployerProfiles.FindAsync(id);
                    sb.AppendLine("COMPANY PROFILE");
                    sb.AppendLine("Company Name: " + (empProfile?.CompanyName ?? "-"));
                    sb.AppendLine("Industry: " + (empProfile?.Industry ?? "-"));
                    sb.AppendLine("Description: " + (empProfile?.CompanyDescription ?? "-"));
                    sb.AppendLine("Address: " + (empProfile?.Address ?? "-"));
                    sb.AppendLine("Website: " + (empProfile?.Website ?? "-"));
                    sb.AppendLine("Verification Status: " + (empProfile?.VerificationStatus ?? "-"));
                    sb.AppendLine();

                    var jobs = await _context.JobVacancies
                        .Where(j => j.EmployerID == id)
                        .OrderByDescending(j => j.PostedDate)
                        .ToListAsync();
                    sb.AppendLine("JOB POSTINGS (" + jobs.Count + ")");
                    foreach (var j in jobs)
                    {
                        sb.AppendLine("  - " + j.JobTitle + " | " + j.Location + " | " + j.EmploymentType + " | Posted " + j.PostedDate.ToString("d MMM yyyy") + " | " + j.Status);
                        sb.AppendLine("    Description: " + j.Description);
                        sb.AppendLine("    Accessibility: " + j.AccessibilityFeatures);
                        sb.AppendLine("    Accommodations: " + j.AccommodationsAvailable);
                    }
                    sb.AppendLine();
                    break;

                case "CareerAdvisor":
                    var advProfile = await _context.CareerAdvisorProfiles.FindAsync(id);
                    sb.AppendLine("ADVISOR PROFILE");
                    sb.AppendLine("Specialisation: " + (advProfile?.Specialisation ?? "-"));
                    sb.AppendLine("Qualification: " + (advProfile?.Qualification ?? "-"));
                    sb.AppendLine("Experience: " + (advProfile?.ExperienceYears.ToString() ?? "-") + " years");
                    sb.AppendLine("Bio: " + (advProfile?.Bio ?? "-"));
                    sb.AppendLine();

                    var resources = await _context.CareerResources
                        .Where(r => r.AdvisorID == id)
                        .OrderByDescending(r => r.PublishedDate)
                        .ToListAsync();
                    sb.AppendLine("PUBLISHED RESOURCES (" + resources.Count + ")");
                    foreach (var r in resources)
                    {
                        sb.AppendLine("  - " + r.Title + " | " + r.Category + " | Published " + r.PublishedDate.ToString("d MMM yyyy") + " | " + r.Status);
                        sb.AppendLine("    Description: " + r.Description);
                        sb.AppendLine("    URL: " + r.ContentURL);
                    }
                    sb.AppendLine();
                    break;

                case "Admin":
                    var announcements = await _context.Announcements
                        .Where(a => a.AdminID == id)
                        .OrderByDescending(a => a.PublishedDate)
                        .ToListAsync();
                    sb.AppendLine("ANNOUNCEMENTS POSTED (" + announcements.Count + ")");
                    foreach (var a in announcements)
                    {
                        sb.AppendLine("  - " + a.Title + " | Published " + a.PublishedDate.ToString("d MMM yyyy") + " | " + a.Status);
                        sb.AppendLine("    Content: " + a.Content);
                    }
                    sb.AppendLine();
                    break;
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var safeFullName = string.Concat(user.FullName.Split(Path.GetInvalidFileNameChars()));
            var fileName = "UserReport_" + safeFullName.Replace(" ", "_") + "_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
            return File(bytes, "text/plain", fileName);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.Status = user.Status == "Active" ? "Suspended" : "Active";
                await _context.SaveChangesAsync();
                TempData["Flash"] = user.FullName + " " + (user.Status == "Active" ? "reactivated." : "suspended.");
            }

            return RedirectToAction("UserAccounts");
        }

        public async Task<IActionResult> EmployerVerification()
        {
            var pending = await _context.EmployerProfiles
                .Where(e => e.VerificationStatus == "Pending")
                .ToListAsync();
            return View(pending);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveEmployer(int id)
        {
            var employer = await _context.EmployerProfiles.FindAsync(id);
            if (employer != null)
            {
                employer.VerificationStatus = "Approved";
                await _context.SaveChangesAsync();
                TempData["Flash"] = employer.CompanyName + " approved. They can post vacancies now.";
            }

            return RedirectToAction("EmployerVerification");
        }

        [HttpPost]
        public async Task<IActionResult> RejectEmployer(int id)
        {
            var employer = await _context.EmployerProfiles.FindAsync(id);
            if (employer != null)
            {
                employer.VerificationStatus = "Rejected";
                await _context.SaveChangesAsync();
                TempData["Flash"] = employer.CompanyName + " rejected.";
            }

            return RedirectToAction("EmployerVerification");
        }

        // ===================== Function 2=====================

        public async Task<IActionResult> JobPostingsMonitor()
        {
            var jobs = await _context.JobVacancies
                .OrderByDescending(j => j.PostedDate)
                .ToListAsync();
            return View(jobs);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteJobPosting(int id, string? returnUrl)
        {
            var job = await _context.JobVacancies.FindAsync(id);
            if (job != null)
            {
                _context.JobVacancies.Remove(job);
                await _context.SaveChangesAsync();
                TempData["Flash"] = job.JobTitle + " removed.";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("JobPostingsMonitor");
        }

        public async Task<IActionResult> EducationalResourcesMonitor()
        {
            var resources = await _context.CareerResources
                .OrderByDescending(r => r.PublishedDate)
                .ToListAsync();
            return View(resources);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteResource(int id, string? returnUrl)
        {
            var resource = await _context.CareerResources.FindAsync(id);
            if (resource != null)
            {
                _context.CareerResources.Remove(resource);
                await _context.SaveChangesAsync();
                TempData["Flash"] = resource.Title + " removed.";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("EducationalResourcesMonitor");
        }

        // ===================== Function 3=====================

        public async Task<IActionResult> SystemSettings()
        {
            var settings = await _context.SystemSettings
                .OrderBy(s => s.SettingCategory)
                .ThenBy(s => s.SettingKey)
                .ToListAsync();
            return View(settings);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSystemSetting(int settingID, string settingValue)
        {
            var setting = await _context.SystemSettings.FindAsync(settingID);
            if (setting != null)
            {
                setting.SettingValue = settingValue;
                setting.UpdatedDate = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["Flash"] = setting.SettingKey + " updated.";
            }

            return RedirectToAction("SystemSettings");
        }

        public async Task<IActionResult> Announcements()
        {
            var announcements = await _context.Announcements
                .OrderByDescending(a => a.PublishedDate)
                .ToListAsync();
            return View(announcements);
        }

        [HttpGet]
        public IActionResult CreateAnnouncement()
        {
            return View();
        }

        // "draft" or "publish
        [HttpPost]
        public async Task<IActionResult> CreateAnnouncement(Announcement model, string action)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.AdminID = HttpContext.Session.GetInt32("UserID") ?? 0;
            model.PublishedDate = DateTime.Now;
            model.Status = action == "draft" ? "Draft" : "Published";

            _context.Announcements.Add(model);
            await _context.SaveChangesAsync();
            TempData["Flash"] = model.Status == "Draft" ? "Announcement saved as draft." : "Announcement published.";

            return RedirectToAction("Announcements");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAnnouncement(int id, string? returnUrl)
        {
            var announcement = await _context.Announcements.FindAsync(id);
            if (announcement != null)
            {
                _context.Announcements.Remove(announcement);
                await _context.SaveChangesAsync();
                TempData["Flash"] = announcement.Title + " removed.";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Announcements");
        }

        [HttpGet]
        public async Task<IActionResult> EditAnnouncement(int id)
        {
            var announcement = await _context.Announcements.FindAsync(id);
            if (announcement == null)
            {
                return NotFound();
            }

            return View(announcement);
        }

        [HttpPost]
        public async Task<IActionResult> EditAnnouncement(int id, Announcement model, string action)
        {
            var announcement = await _context.Announcements.FindAsync(id);
            if (announcement == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                model.AnnouncementID = id;
                return View(model);
            }

            announcement.Title = model.Title;
            announcement.Content = model.Content;
            announcement.Status = action == "draft" ? "Draft" : "Published";

            await _context.SaveChangesAsync();
            TempData["Flash"] = announcement.Status == "Draft" ? "Announcement saved as draft." : "Announcement updated and published.";

            return RedirectToAction("Announcements");
        }

        // ===================== Function 4=====================

        public async Task<IActionResult> Reports()
        {
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.ActiveUsers = await _context.Users.CountAsync(u => u.Status == "Active");
            ViewBag.SuspendedUsers = await _context.Users.CountAsync(u => u.Status == "Suspended");

            ViewBag.TotalEmployers = await _context.EmployerProfiles.CountAsync();
            ViewBag.ApprovedEmployers = await _context.EmployerProfiles.CountAsync(e => e.VerificationStatus == "Approved");
            ViewBag.PendingEmployers = await _context.EmployerProfiles.CountAsync(e => e.VerificationStatus == "Pending");

            ViewBag.TotalJobPostings = await _context.JobVacancies.CountAsync();
            ViewBag.OpenJobPostings = await _context.JobVacancies.CountAsync(j => j.Status == "Open");

            return View();
        }

        // Downloadable platform-wide report - user activity, employer
        // verification, and employment statistics in one file.
        public async Task<IActionResult> GenerateEmploymentReport()
        {
            var totalUsers = await _context.Users.CountAsync();
            var activeUsers = await _context.Users.CountAsync(u => u.Status == "Active");
            var suspendedUsers = await _context.Users.CountAsync(u => u.Status == "Suspended");

            var totalEmployers = await _context.EmployerProfiles.CountAsync();
            var approvedEmployers = await _context.EmployerProfiles.CountAsync(e => e.VerificationStatus == "Approved");
            var pendingEmployers = await _context.EmployerProfiles.CountAsync(e => e.VerificationStatus == "Pending");
            var rejectedEmployers = await _context.EmployerProfiles.CountAsync(e => e.VerificationStatus == "Rejected");

            var totalJobPostings = await _context.JobVacancies.CountAsync();
            var openJobPostings = await _context.JobVacancies.CountAsync(j => j.Status == "Open");
            var draftJobPostings = await _context.JobVacancies.CountAsync(j => j.Status == "Draft");
            var closedJobPostings = await _context.JobVacancies.CountAsync(j => j.Status == "Closed");

            var totalApplications = await _context.JobApplications.CountAsync();

            var sb = new StringBuilder();
            sb.AppendLine("EMPLOYMENT STATISTICS REPORT");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("d MMM yyyy, HH:mm"));
            sb.AppendLine(new string('=', 60));
            sb.AppendLine();

            sb.AppendLine("USER ACTIVITY");
            sb.AppendLine("Total users: " + totalUsers);
            sb.AppendLine("Active: " + activeUsers);
            sb.AppendLine("Suspended: " + suspendedUsers);
            sb.AppendLine();

            sb.AppendLine("EMPLOYER VERIFICATION");
            sb.AppendLine("Total employers: " + totalEmployers);
            sb.AppendLine("Approved: " + approvedEmployers);
            sb.AppendLine("Pending: " + pendingEmployers);
            sb.AppendLine("Rejected: " + rejectedEmployers);
            sb.AppendLine();

            sb.AppendLine("EMPLOYMENT STATISTICS");
            sb.AppendLine("Total job postings: " + totalJobPostings);
            sb.AppendLine("Open: " + openJobPostings);
            sb.AppendLine("Draft: " + draftJobPostings);
            sb.AppendLine("Closed: " + closedJobPostings);
            sb.AppendLine("Total applications submitted: " + totalApplications);

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = "EmploymentReport_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".txt";
            return File(bytes, "text/plain", fileName);
        }
    }
}
