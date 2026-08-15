using DDAC.Data;
using DDAC.Models;
using Microsoft.AspNetCore.Mvc;

namespace DDAC.Controllers
{
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(User user, string password)
        {
            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine(error.ErrorMessage);
                }

                return View(user);
            }

            user.Password = password;
            user.Status = "Active";
            user.CreatedDate = DateTime.Now;

            _context.Users.Add(user);
            _context.SaveChanges();

            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string email, string password)
        {
            var user = _context.Users
                .FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View();
            }

            if (user.Password != password)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View();
            }

            if (user.Status != "Active")
            {
                ModelState.AddModelError("", "Your account is not active.");
                return View();
            }

            HttpContext.Session.SetInt32("UserID", user.UserID);
            HttpContext.Session.SetString("FullName", user.FullName);
            HttpContext.Session.SetString("Email", user.Email);
            HttpContext.Session.SetString("Role", user.Role);

            if (user.Role == "JobSeeker")
            {
                return RedirectToAction("Announcements", "JobSeeker");
            }
            else if (user.Role == "Employer")
            {
                return RedirectToAction("Index", "Employer");
            }
            else if (user.Role == "CareerAdvisor")
            {
                return RedirectToAction("Index", "CareerAdvisor");
            }
            else if (user.Role == "Admin")
            {
                return RedirectToAction("Index", "Admin");
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            return RedirectToAction("Login", "User");
        }

    }
}