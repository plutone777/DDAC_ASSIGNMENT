using DDAC.Controllers;
using DDAC.Data;
using DDAC.Models;
using DDAC.Services;
using DDAC.Services.Employer;
using DDAC.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DDAC.Employer.Tests;

internal sealed class LocalBaselineFixture(bool startInterviewApi = true) : WebApplicationFactory<UserController>
{
    private InterviewHostTests.RunningHost? interviewApi;
    internal DelegatingHandler? ApiObserver { get; init; }
    internal string CallerKey => interviewApi?.Key ?? "";
    internal const string Connection = @"Server=(localdb)\MSSQLLocalDB;Database=DDAC_Employer_LocalTest;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=10;";
    internal string RunLabel { get; } = "LOCAL TEST AUTO " + Guid.NewGuid().ToString("N");
    internal string Password { get; } = Guid.NewGuid().ToString("N");
    internal User Owner { get; private set; } = null!;
    internal User Other { get; private set; } = null!;
    internal User Advisor { get; private set; } = null!;
    internal JobVacancy Vacancy { get; private set; } = null!;
    internal JobApplication Submitted { get; private set; } = null!;
    internal JobApplication UnderReview { get; private set; } = null!;
    internal Inquiry Inquiry { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(Connection));
            services.RemoveAll<S3Service>();
            services.AddScoped<S3Service>(_ => throw new InvalidOperationException("S3 is forbidden in local baseline tests."));
            if (interviewApi is not null)
            {
                // In-memory runtime configuration only; production client/HTTP transport remain real.
                var url = interviewApi.Client.BaseAddress!.AbsoluteUri;
                var key = interviewApi.Key;
                services.PostConfigure<EmployerInterviewOptions>(options =>
                {
                    options.BaseUrl = url;
                    options.CallerKey = key;
                });
            }
            if (ApiObserver is not null)
                services.AddHttpClient<IInterviewApiClient, InterviewApiClient>()
                    .AddHttpMessageHandler(() => ApiObserver);
        });
    }

    internal ApplicationDbContext OpenDb() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(Connection).Options);

    internal HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    internal async Task InitializeAsync()
    {
        // No environment-supplied connection, database creation, migration, or schema mutation.
        await using var connection = new SqlConnection(Connection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN DB_NAME() = N'DDAC_Employer_LocalTest' AND CONVERT(int, SERVERPROPERTY('IsLocalDB')) = 1 THEN 1 ELSE 0 END";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("Refusing a non-approved local database target.");

        await using var db = OpenDb();
        var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        if (!migrations.SequenceEqual(new[] { "20260814024546_InitialCreate" }))
            throw new InvalidOperationException("Unexpected local migration history; no schema changes attempted.");
        if (db.Database.HasPendingModelChanges())
            throw new InvalidOperationException("Model differs from migration snapshot; stop for review.");
        if (startInterviewApi) interviewApi = await InterviewHostTests.RunningHost.StartAsync();
        using (var scope = Services.CreateScope())
        {
            var actual = new SqlConnectionStringBuilder(scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>().Database.GetConnectionString());
            if (actual.DataSource != @"(localdb)\MSSQLLocalDB" || actual.InitialCatalog != "DDAC_Employer_LocalTest" || !actual.IntegratedSecurity)
                throw new InvalidOperationException("Test host target mismatch.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        User MakeUser(string role, string suffix) => new()
        {
            Role = role, FullName = RunLabel + " " + suffix,
            Email = Guid.NewGuid().ToString("N") + "@example.invalid",
            Password = Password, Status = "Active", CreatedDate = DateTime.Now
        };
        Owner = MakeUser("Employer", "Owner");
        Other = MakeUser("Employer", "Other");
        Advisor = MakeUser("CareerAdvisor", "Advisor");
        var seeker = MakeUser("JobSeeker", "Seeker");
        db.Users.AddRange(Owner, Other, Advisor, seeker);
        await db.SaveChangesAsync();
        db.EmployerProfiles.Add(new EmployerProfile
        {
            EmployerID = Owner.UserID, CompanyName = RunLabel, Industry = "Testing",
            CompanyDescription = RunLabel, Address = "LOCAL TEST Office", Website = "https://example.invalid",
            VerificationStatus = "Pending"
        });
        db.JobSeekerProfiles.Add(new JobSeekerProfile { JobSeekerID = seeker.UserID, Bio = RunLabel, ResumeURL = "LOCAL-TEST-NOT-A-RESUME" });
        db.CareerAdvisorProfiles.Add(new CareerAdvisorProfile
        {
            AdvisorID = Advisor.UserID, Specialisation = "LOCAL TEST", Qualification = "LOCAL TEST", Bio = RunLabel, ExperienceYears = 1
        });
        Vacancy = new JobVacancy
        {
            EmployerID = Owner.UserID, JobTitle = RunLabel, Description = RunLabel, Location = "LOCAL TEST Office",
            EmploymentType = "Full-time", Salary = 3000, AccessibilityFeatures = "LOCAL TEST",
            AccommodationsAvailable = "LOCAL TEST", PostedDate = DateTime.Now,
            ClosingDate = DateTime.Today.AddDays(30), Status = "Published"
        };
        db.JobVacancies.Add(Vacancy);
        await db.SaveChangesAsync();
        JobApplication MakeApplication(string status) => new()
        {
            JobID = Vacancy.JobID, JobSeekerID = seeker.UserID, ApplicationDate = DateTime.Now,
            ResumeURL = "LOCAL-TEST-NOT-A-RESUME", CoverLetter = RunLabel, Status = status
        };
        Submitted = MakeApplication("Submitted");
        UnderReview = MakeApplication("Under Review");
        Inquiry = new Inquiry { UserID = Owner.UserID, AdvisorID = Advisor.UserID, Subject = RunLabel, Message = RunLabel, Status = "Open", CreatedDate = DateTime.Now };
        db.JobApplications.AddRange(Submitted, UnderReview);
        db.Inquiries.Add(Inquiry);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    internal async Task<string> BusinessSnapshotAsync()
    {
        // Includes attempted inserts for both test Employers; excludes credentials and unrelated users.
        await using var db = OpenDb();
        var owners = new[] { Owner.UserID, Other.UserID };
        var applications = new[] { Submitted.ApplicationID, UnderReview.ApplicationID };
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            Profiles = await db.EmployerProfiles.AsNoTracking().Where(x => owners.Contains(x.EmployerID)).OrderBy(x => x.EmployerID).ToListAsync(),
            Vacancies = await db.JobVacancies.AsNoTracking().Where(x => owners.Contains(x.EmployerID)).OrderBy(x => x.JobID).ToListAsync(),
            Applications = await db.JobApplications.AsNoTracking().Where(x => x.JobID == Vacancy.JobID).OrderBy(x => x.ApplicationID).ToListAsync(),
            Interviews = await db.JobInterviews.AsNoTracking().Where(x => applications.Contains(x.ApplicationID)).OrderBy(x => x.InterviewID).ToListAsync(),
            Inquiries = await db.Inquiries.AsNoTracking().Where(x => owners.Contains(x.UserID)).OrderBy(x => x.InquiryID).ToListAsync()
        });
    }

    internal async Task StopInterviewApiAsync()
    {
        var child = interviewApi;
        interviewApi = null;
        if (child is not null) await child.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        try { base.Dispose(disposing); }
        finally { if (disposing) StopInterviewApiAsync().GetAwaiter().GetResult(); }
    }

    public override async ValueTask DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally { await StopInterviewApiAsync(); }
    }
}
