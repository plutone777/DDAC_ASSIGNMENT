using DDAC.Data;
using DDAC.Services.Employer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DDAC.Employer.Interviews.Lambda;

internal interface ISchedulingInvocation : IAsyncDisposable
{
    IInterviewSchedulingService Service { get; }
    Task<bool> IsActiveEmployerAsync(int employerId);
}

internal sealed class SchedulingInvocation : ISchedulingInvocation
{
    private readonly ApplicationDbContext database;
    public IInterviewSchedulingService Service { get; }

    internal SchedulingInvocation()
    {
        // Environment only: no appsettings, credentials file, migration or database creation.
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var connection = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Database configuration is required.");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection).Options;
        database = new ApplicationDbContext(options);
        Service = new InterviewSchedulingService(database);
    }

    public Task<bool> IsActiveEmployerAsync(int employerId) => database.Users.AsNoTracking()
        .AnyAsync(user => user.UserID == employerId && user.Role == "Employer" && user.Status == "Active");

    public ValueTask DisposeAsync() => database.DisposeAsync();
}
