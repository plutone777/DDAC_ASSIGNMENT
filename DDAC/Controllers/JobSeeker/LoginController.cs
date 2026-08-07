using DDAC.Models.JobSeeker;
using Microsoft.AspNetCore.Mvc;

namespace InclusiveEmployment.Controllers
{
    public class JobSeekerController : Controller
    {
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}