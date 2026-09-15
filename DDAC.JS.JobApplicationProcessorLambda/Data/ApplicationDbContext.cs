using Microsoft.EntityFrameworkCore;
using DDAC.JS.JobApplicationProcessorLambda.Models;

namespace DDAC.JS.JobApplicationProcessorLambda.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<JobVacancy> JobVacancies { get; set; }
    public DbSet<JobSeekerProfile> JobSeekerProfiles { get; set; }
    public DbSet<JobApplication> JobApplications { get; set; }
}