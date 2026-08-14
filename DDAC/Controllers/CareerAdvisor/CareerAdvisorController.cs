using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DDAC.Data;
using DDAC.Models;

namespace DDAC.Controllers.CareerAdvisor
{
    public class CareerAdvisorController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CareerAdvisorController(ApplicationDbContext context)
        {
            _context = context;
        }



        //Career resources

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null)
            {
                return RedirectToAction("Login", "User");
            }
            var resources = await _context.CareerResources
                .Where(r => r.AdvisorID == userId.Value)
                .ToListAsync();
            return View(resources);
        }

        

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CareerResource resource)
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null)
            {
                return RedirectToAction("Login", "User");
            }

            resource.AdvisorID = userId.Value;
            resource.PublishedDate = DateTime.Now;
            resource.Status = "Published";
            _context.CareerResources.Add(resource);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");

        }


        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var resource = await _context.CareerResources.FindAsync(id);
            if (resource == null)
            {
                return NotFound();
            }
            return View(resource);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CareerResource resource)
        {
            var existing = await _context.CareerResources.FindAsync(resource.ResourceID);
            if (existing == null)
            {
                return NotFound();
            }

            existing.Title = resource.Title;
            existing.Description = resource.Description;
            existing.Category = resource.Category;
            existing.ContentURL = resource.ContentURL;

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }


        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var resource = await _context.CareerResources.FindAsync(id);
            if (resource == null)
            {
                return NotFound();
            }
            return View(resource);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var resource = await _context.CareerResources.FindAsync(id);
            if (resource == null)
            {
                return NotFound();
            }

            _context.CareerResources.Remove(resource);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }



        //Training programs

        [HttpGet]
        public async Task<IActionResult> TrainingPrograms()
        {
            var programs = await _context.TrainingPrograms.ToListAsync();
            return View(programs);
        }

        [HttpGet]
        public IActionResult CreateTraining()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTraining(TrainingProgram program)
        {
            program.Status = "Active";

            _context.TrainingPrograms.Add(program);
            await _context.SaveChangesAsync();
            return RedirectToAction("TrainingPrograms");
        }



        [HttpGet]
        public async Task<IActionResult> EditTraining(int id)
        {
            var program = await _context.TrainingPrograms.FindAsync(id);
            if (program == null)
            {
                return NotFound();
            }
            return View(program);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTraining(TrainingProgram program)
        {
            var existing = await _context.TrainingPrograms.FindAsync(program.TrainingID);
            if (existing == null)
            {
                return NotFound();
            }

            existing.Title = program.Title;
            existing.Provider = program.Provider;
            existing.Description = program.Description;
            existing.Eligibility = program.Eligibility;
            existing.URL = program.URL;

            await _context.SaveChangesAsync();
            return RedirectToAction("TrainingPrograms");
        }



        [HttpGet]
        public async Task<IActionResult> DeleteTraining(int id)
        {
            var program = await _context.TrainingPrograms.FindAsync(id);
            if (program == null)
            {
                return NotFound();
            }
            return View(program);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTrainingConfirmed(int id)
        {
            var program = await _context.TrainingPrograms.FindAsync(id);
            if (program == null)
            {
                return NotFound();
            }

            _context.TrainingPrograms.Remove(program);
            await _context.SaveChangesAsync();

            return RedirectToAction("TrainingPrograms");
        }



        //Inquiries

        [HttpGet]
        public async Task<IActionResult> Inquiries()
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null)
            {
                return RedirectToAction("Login", "User");
            }

            var inquiries = await _context.Inquiries
                .Where(i => i.AdvisorID == userId.Value)
                .ToListAsync();

            return View(inquiries);
        }



        [HttpGet]
        public async Task<IActionResult> RespondInquiry(int id)
        {
            var inquiry = await _context.Inquiries.FindAsync(id);
            if (inquiry == null)
            {
                return NotFound();
            }

            return View(inquiry);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RespondInquiry(Inquiry inquiry)
        {
            var existing = await _context.Inquiries.FindAsync(inquiry.InquiryID);

            if (existing == null)
            {
                return NotFound();
            }

            existing.Response = inquiry.Response;
            existing.Status = "Resolved";
            existing.ResolvedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            return RedirectToAction("Inquiries");
        }


        //Recommendations

        [HttpGet]
        public async Task<IActionResult> Recommendations()
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null)
            {
                return RedirectToAction("Login", "User");
            }
            var recommendations = await _context.CareerRecommendations
                .Where(r => r.AdvisorID == userId.Value)
                .ToListAsync();
            ViewBag.Names = await _context.Users
                .Where(u => u.Role == "JobSeeker")
                .ToDictionaryAsync(u => u.UserID, u => u.FullName);
            return View(recommendations);

        }

        [HttpGet]
        public async Task<IActionResult> CreateRecommendation()
        {
            ViewBag.JobSeekers = await _context.Users
                .Where(u => u.Role == "JobSeeker")
                .ToListAsync();
            ViewBag.Programs = await _context.TrainingPrograms.ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRecommendation(int jobSeekerId, int trainingId, string reason)
        {
            var userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null)
            {
                return RedirectToAction("Login", "User");
            }

            var program = await _context.TrainingPrograms.FindAsync(trainingId);
            if (program == null)
            {
                return NotFound();
            }

            var recommendation = new CareerRecommendation
            {
                AdvisorID = userId.Value,
                JobSeekerID = jobSeekerId,
                RecommendationType = "Training",
                Title = program.Title,
                Description = program.Description,
                Reason = reason,
                DateCreated = DateTime.Now
            };

            _context.CareerRecommendations.Add(recommendation);
            await _context.SaveChangesAsync();
            return RedirectToAction("Recommendations");
        }


        [HttpGet]
        public async Task<IActionResult> DeleteRecommendation(int id)
        {
            var recommendation = await _context.CareerRecommendations.FindAsync(id);
            if (recommendation == null)
            {
                return NotFound();
            }
            return View(recommendation);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRecommendationConfirmed(int id)
        {
            var recommendation = await _context.CareerRecommendations.FindAsync(id);
            if (recommendation == null)
            {
                return NotFound();
            }

            _context.CareerRecommendations.Remove(recommendation);
            await _context.SaveChangesAsync();
            return RedirectToAction("Recommendations");
        }


    }

}
