using DDAC.Data;
using DDAC.Options;
using DDAC.Services;
using DDAC.Services.Employer;
using Microsoft.EntityFrameworkCore;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using Amazon.XRay.Recorder.Handlers.EntityFramework;

var builder = WebApplication.CreateBuilder(args);

// AWS X-Ray
AWSXRayRecorder.InitializeInstance(builder.Configuration);
AWSSDKHandler.RegisterXRayForAllServices();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"))
        .AddXRayInterceptor());

builder.Services.AddControllersWithViews();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();

builder.Services.AddScoped<S3Service>();

// Job Application Service
builder.Services.AddScoped<JobApplicationService>();

// Employer Interview Services
builder.Services.AddScoped<IInterviewSchedulingService, InterviewSchedulingService>();

builder.Services.Configure<EmployerInterviewOptions>(options =>
{
    options.BaseUrl =
        Environment.GetEnvironmentVariable("EmployerInterview__BaseUrl") ?? "";

    options.CallerKey =
        Environment.GetEnvironmentVariable("EmployerInterview__CallerKey") ?? "";
});

builder.Services.AddHttpClient<IInterviewApiClient, InterviewApiClient>(
    client => client.Timeout = TimeSpan.FromSeconds(15))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false
    })
    .RemoveAllLoggers();

// General HttpClient
builder.Services.AddHttpClient();

var app = builder.Build();

// AWS X-Ray
app.UseXRay("DDACJobPortal");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=User}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();