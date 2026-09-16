using DDAC.Data;
using DDAC.Services;
using Microsoft.EntityFrameworkCore;
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using Amazon.XRay.Recorder.Handlers.EntityFramework;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddHttpClient();

var app = builder.Build();
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