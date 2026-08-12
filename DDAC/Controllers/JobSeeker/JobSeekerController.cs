using Microsoft.AspNetCore.Mvc;

namespace DDAC.Controllers.JobSeeker
{
    public class JobSeekerController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
