using DDAC.Models;
using Microsoft.EntityFrameworkCore;

namespace DDAC.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<JobSeekerProfile> JobSeekerProfiles { get; set; }
        public DbSet<JobVacancy> JobVacancies { get; set; }
        public DbSet<JobApplication> JobApplications { get; set; }
    }
}