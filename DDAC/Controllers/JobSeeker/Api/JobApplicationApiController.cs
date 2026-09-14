using DDAC.Models;
using DDAC.Services;
using Microsoft.AspNetCore.Mvc;

namespace DDAC.Controllers.Api
{
    [ApiController]
    [Route("api/jobapplications")]
    public class JobApplicationApiController : ControllerBase
    {
        private readonly JobApplicationService _jobApplicationService;

        public JobApplicationApiController(
            JobApplicationService jobApplicationService)
        {
            _jobApplicationService = jobApplicationService;
        }

        [HttpPost]
        public async Task<IActionResult> SubmitApplication(
            [FromBody] JobApplicationRequest request)
        {
            var result = await _jobApplicationService.SubmitApplicationAsync(
                request.JobSeekerID,
                request.JobID,
                request.CoverLetter);

            if (!result.Success)
            {
                return BadRequest(new
                {
                    message = result.Message
                });
            }

            return Ok(result.Application);
        }
    }

    public class JobApplicationRequest
    {
        public int JobID { get; set; }
        public int JobSeekerID { get; set; }
        public string CoverLetter { get; set; }
    }
}
