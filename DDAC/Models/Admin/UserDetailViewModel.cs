using DDAC.Models;

namespace DDAC.Models.Admin
{
    // Admin-only view model - bundles a User with whatever role-specific
    // data actually exists for them. Not a database table.
    public class UserDetailViewModel
    {
        public User User { get; set; } = null!;

        // ---- Job Seeker ----
        public JobSeekerProfile? JobSeekerProfile { get; set; }
        public List<string> Skills { get; set; } = new();
        public List<JobSeekerQualification> Qualifications { get; set; } = new();
        public List<ApplicationSummary> Applications { get; set; } = new();

        // ---- Employer ----
        public EmployerProfile? EmployerProfile { get; set; }
        public List<JobVacancy> JobPostings { get; set; } = new();

        // ---- Career Advisor ----
        public CareerAdvisorProfile? CareerAdvisorProfile { get; set; }
        public List<CareerResource> PublishedResources { get; set; } = new();

        // ---- Admin ----
        public List<Announcement> Announcements { get; set; } = new();
    }

    public class ApplicationSummary
    {
        public string JobTitle { get; set; } = string.Empty;
        public DateTime ApplicationDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ResumeURL { get; set; }
        public string? CoverLetter { get; set; }
    }

    // Admin-only - one row in the User Accounts list, with how many
    // "posts" that person has (applications/postings/resources/announcements
    // depending on their role).
    public class UserAccountRow
    {
        public User User { get; set; } = null!;
        public int PostCount { get; set; }
    }
}
