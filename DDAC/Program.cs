using DDAC.Data;
using DDAC.Options;
using DDAC.Services;
using DDAC.Services.Employer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllersWithViews();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();

builder.Services.AddScoped<S3Service>();
builder.Services.AddScoped<IInterviewSchedulingService, InterviewSchedulingService>();
builder.Services.Configure<EmployerInterviewOptions>(builder.Configuration.GetSection("EmployerInterview"));
builder.Services.AddHttpClient<IInterviewApiClient, InterviewApiClient>(client => client.Timeout = TimeSpan.FromSeconds(5))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false })
    .RemoveAllLoggers();

var app = builder.Build();

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
