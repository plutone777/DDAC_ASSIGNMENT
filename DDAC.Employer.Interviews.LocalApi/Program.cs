using DDAC.Data;
using DDAC.Services.Employer;
using DDAC.Employer.Interviews.LocalApi;
using Microsoft.EntityFrameworkCore;

// LOCAL proof only. Never load the MVC application's appsettings or cloud credentials.
var secret = Environment.GetEnvironmentVariable("DDAC_INTERVIEWS_CALLER_KEY");
var portText = Environment.GetEnvironmentVariable("DDAC_INTERVIEWS_PORT") ?? "5244";
if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32 ||
    !int.TryParse(portText, out var port) || port is < 1024 or > 65535)
{
    Console.Error.WriteLine("StartupPhase: CallerConfigurationValidation");
    Console.Error.WriteLine("Local API requires a caller key of at least 32 characters and a valid local port.");
    return 1;
}

var startupPhase = "ConfigurationLoading";
try
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
    // Preserve framework host settings; local URL and database targets are set explicitly below.
    startupPhase = "LoggingConfiguration";
    builder.Logging.ClearProviders(); // No credentials, SQL, payloads or exception details in logs.
    startupPhase = "UrlConfiguration";
    builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
    startupPhase = "ServiceRegistration";
    builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(LocalDatabase.Connection));
    builder.Services.AddScoped<IInterviewSchedulingService, InterviewSchedulingService>();
    startupPhase = "ApplicationBuild";
    var app = builder.Build();

    // Fail closed before listening; no EnsureCreated, migrations or schema mutation.
    startupPhase = "LocalDatabaseValidation";
    await LocalDatabase.VerifyAsync();
    startupPhase = "EndpointRegistration";
    app.Use(async (context, next) =>
    {
        try { await next(context); }
        catch
        {
            if (context.Response.HasStarted) { context.Abort(); return; }
            context.Response.Clear();
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(SchedulingEndpoint.Failure("internal_error", "Unable to schedule interview."));
        }
    });
    app.MapPost("/api/interviews/schedule", (HttpContext context, ApplicationDbContext db,
        IInterviewSchedulingService service) => SchedulingEndpoint.HandleAsync(context, db, service, secret));
    startupPhase = "ApplicationStartListen";
    await app.RunAsync();
    return 0;
}
catch (Exception exception)
{
    // Startup diagnostics expose labels and exception type only, never Message/ToString/configuration.
    Console.Error.WriteLine($"StartupPhase: {startupPhase}");
    Console.Error.WriteLine($"ExceptionType: {exception.GetType().FullName}");
    Console.Error.WriteLine("Local API could not start or stopped unexpectedly. Verify the local configuration and database.");
    return 1;
}
