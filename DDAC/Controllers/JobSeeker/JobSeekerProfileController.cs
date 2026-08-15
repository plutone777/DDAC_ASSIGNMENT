using DDAC.Data;
using DDAC.Models;
using DDAC.Services;
using Microsoft.AspNetCore.Mvc;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;

namespace DDAC.Controllers.JobSeeker
{
    public class JobSeekerProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly S3Service _s3Service;

        public JobSeekerProfileController(
            ApplicationDbContext context,
            S3Service s3Service)
        {
            _context = context;
            _s3Service = s3Service;
        }

        [HttpGet]
        public IActionResult EditProfile()
        {
            var userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null)
            {
                return RedirectToAction("Login", "User");
            }

            var profile = _context.JobSeekerProfiles
                .FirstOrDefault(p =>
                    p.JobSeekerID == userId.Value);

            if (profile == null)
            {
                profile = new JobSeekerProfile
                {
                    JobSeekerID = userId.Value
                };
            }

            var userSkills = _context.JobSeekerSkills
                .Where(js =>
                    js.JobSeekerID == userId.Value)
                .Join(
                    _context.Skills,
                    js => js.SkillID,
                    s => s.SkillID,
                    (js, s) => new
                    {
                        js.SkillID,
                        s.SkillName,
                        js.SkillLevel
                    })
                .ToList();

            ViewBag.UserSkills = userSkills;

            var qualifications = _context.Qualifications
                .Where(q =>
                    q.JobSeekerID == userId.Value)
                .OrderByDescending(q =>
                    q.CompletionYear)
                .ToList();

            ViewBag.Qualifications = qualifications;

            return View(
                "~/Views/JobSeeker/EditProfile.cshtml",
                profile
            );
        }

        [HttpGet]
        public IActionResult SearchSkills(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Json(new List<object>());
            }

            var skills = _context.Skills
                .Where(s =>
                    s.SkillName.Contains(term))
                .OrderBy(s =>
                    s.SkillName)
                .Take(10)
                .Select(s => new
                {
                    id = s.SkillID,
                    name = s.SkillName
                })
                .ToList();

            return Json(skills);
        }

        [HttpGet]
        public async Task<IActionResult> ViewResume()
        {
            var userId =
                HttpContext.Session.GetInt32("UserID");

            if (userId == null)
            {
                return RedirectToAction(
                    "Login",
                    "User");
            }

            var profile =
                await _context.JobSeekerProfiles
                    .FirstOrDefaultAsync(p =>
                        p.JobSeekerID == userId.Value);

            if (profile == null ||
                string.IsNullOrEmpty(profile.ResumeURL))
            {
                return NotFound("Resume not found.");
            }

            var url =
                await _s3Service.GetResumeUrlAsync(
                    profile.ResumeURL);

            return Redirect(url);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(
            JobSeekerProfile profile,
            IFormFile? resume,
            List<int>? skillIDs,
            List<string>? skillNames,
            List<string>? skillLevels,
            List<string>? qualificationNames,
            List<string>? institutions,
            List<int>? completionYears)
        {
            var userId =
                HttpContext.Session.GetInt32("UserID");

            if (userId == null)
            {
                return RedirectToAction(
                    "Login",
                    "User");
            }

            profile.JobSeekerID = userId.Value;

            var existingProfile =
                await _context.JobSeekerProfiles
                    .FirstOrDefaultAsync(p =>
                        p.JobSeekerID == userId.Value);

            if (resume != null &&
                resume.Length > 0)
            {
                var allowedExtensions = new[]
                {
                    ".pdf",
                    ".doc",
                    ".docx"
                };

                var extension =
                    Path.GetExtension(
                        resume.FileName)
                    .ToLowerInvariant();

                if (!allowedExtensions.Contains(
                        extension))
                {
                    ModelState.AddModelError(
                        "resume",
                        "Only PDF, DOC, and DOCX files are allowed."
                    );

                    return View(
                        "~/Views/JobSeeker/EditProfile.cshtml",
                        profile
                    );
                }

                if (resume.Length >
                    5 * 1024 * 1024)
                {
                    ModelState.AddModelError(
                        "resume",
                        "The resume must be smaller than 5 MB."
                    );

                    return View(
                        "~/Views/JobSeeker/EditProfile.cshtml",
                        profile
                    );
                }

                try
                {
                    var s3Key =
                        await _s3Service.UploadResumeAsync(
                            resume,
                            userId.Value);

                    profile.ResumeURL = s3Key;
                }
                catch (AmazonS3Exception ex)
                {
                    ModelState.AddModelError(
                        "resume",
                        "Unable to upload resume to S3: "
                        + ex.Message
                    );

                    return View(
                        "~/Views/JobSeeker/EditProfile.cshtml",
                        profile
                    );
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(
                        "resume",
                        "An error occurred while uploading the resume: "
                        + ex.Message
                    );

                    return View(
                        "~/Views/JobSeeker/EditProfile.cshtml",
                        profile
                    );
                }
            }

            if (existingProfile == null)
            {
                _context.JobSeekerProfiles.Add(profile);
            }
            else
            {
                existingProfile.CareerGoal =
                    profile.CareerGoal;

                existingProfile.Bio =
                    profile.Bio;

                existingProfile.PreferredLocation =
                    profile.PreferredLocation;

                existingProfile.AccommodationNeeds =
                    profile.AccommodationNeeds;

                if (!string.IsNullOrEmpty(
                        profile.ResumeURL))
                {
                    existingProfile.ResumeURL =
                        profile.ResumeURL;
                }
            }

            var existingSkills =
                await _context.JobSeekerSkills
                    .Where(s =>
                        s.JobSeekerID == userId.Value)
                    .ToListAsync();

            _context.JobSeekerSkills.RemoveRange(
                existingSkills
            );

            if (skillNames != null &&
                skillLevels != null)
            {
                var skillCount =
                    Math.Min(
                        skillNames.Count,
                        skillLevels.Count
                    );

                for (int i = 0;
                     i < skillCount;
                     i++)
                {
                    var skillName =
                        skillNames[i]?.Trim();

                    var skillLevel =
                        skillLevels[i]?.Trim();

                    if (string.IsNullOrWhiteSpace(
                            skillName) ||
                        string.IsNullOrWhiteSpace(
                            skillLevel))
                    {
                        continue;
                    }

                    var skill =
                        await _context.Skills
                            .FirstOrDefaultAsync(s =>
                                s.SkillName.ToLower() ==
                                skillName.ToLower());

                    if (skill == null)
                    {
                        skill = new Skill
                        {
                            SkillName = skillName
                        };

                        _context.Skills.Add(skill);

                        await _context.SaveChangesAsync();
                    }

                    var jobSeekerSkill =
                        new JobSeekerSkill
                        {
                            JobSeekerID =
                                userId.Value,

                            SkillID =
                                skill.SkillID,

                            SkillLevel =
                                skillLevel
                        };

                    _context.JobSeekerSkills.Add(
                        jobSeekerSkill
                    );
                }
            }

            var existingQualifications =
                await _context.Qualifications
                    .Where(q =>
                        q.JobSeekerID == userId.Value)
                    .ToListAsync();

            _context.Qualifications.RemoveRange(
                existingQualifications
            );

            if (qualificationNames != null &&
                institutions != null &&
                completionYears != null)
            {
                var qualificationCount =
                    Math.Min(
                        qualificationNames.Count,
                        Math.Min(
                            institutions.Count,
                            completionYears.Count
                        )
                    );

                for (int i = 0;
                     i < qualificationCount;
                     i++)
                {
                    var qualificationName =
                        qualificationNames[i]?.Trim();

                    var institution =
                        institutions[i]?.Trim();

                    var completionYear =
                        completionYears[i];

                    if (string.IsNullOrWhiteSpace(
                            qualificationName) ||
                        string.IsNullOrWhiteSpace(
                            institution) ||
                        completionYear <= 0)
                    {
                        continue;
                    }

                    var qualification =
                        new JobSeekerQualification
                        {
                            JobSeekerID =
                                userId.Value,

                            QualificationName =
                                qualificationName,

                            Institution =
                                institution,

                            CompletionYear =
                                completionYear
                        };

                    _context.Qualifications.Add(
                        qualification
                    );
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(
                "EditProfile"
            );
        }
    }
}
