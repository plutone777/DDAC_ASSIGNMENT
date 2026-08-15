using Microsoft.EntityFrameworkCore;
using DDAC.Models;

namespace DDAC.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<JobSeekerProfile> JobSeekerProfiles { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<JobSeekerSkill> JobSeekerSkills { get; set; }
        public DbSet<JobSeekerQualification> Qualifications { get; set; }
        public DbSet<EmployerProfile> EmployerProfiles { get; set; }
        public DbSet<JobVacancy> JobVacancies { get; set; }
        public DbSet<JobApplication> JobApplications { get; set; }
        public DbSet<JobInterview> JobInterviews { get; set; }
        public DbSet<CareerAdvisorProfile> CareerAdvisorProfiles { get; set; }
        public DbSet<CareerResource> CareerResources { get; set; }
        public DbSet<Inquiry> Inquiries { get; set; }
        public DbSet<TrainingProgram> TrainingPrograms { get; set; }
        public DbSet<CareerRecommendation> CareerRecommendations { get; set; }
        public DbSet<CareerGuidance> CareerGuidances { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<JobSeekerSkill>()
                .HasKey(jss => new { jss.JobSeekerID, jss.SkillID });
        }
    }
}