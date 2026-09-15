namespace DDAC.JS.JobApplicationProcessorLambda.Models;

public class JobApplicationRequest
{
    public int JobID { get; set; }
    public int JobSeekerID { get; set; }
    public string CoverLetter { get; set; }
}