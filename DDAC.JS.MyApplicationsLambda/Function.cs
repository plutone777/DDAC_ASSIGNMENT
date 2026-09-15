using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using DDAC.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

[assembly: LambdaSerializer(
    typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer)
)]

namespace DDAC.JS.MyApplicationsLambda;

public class Function
{
    private readonly ApplicationDbContext _context;

    public Function()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(
                    configuration.GetConnectionString(
                        "DefaultConnection"))
                .Options;

        _context = new ApplicationDbContext(options);
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(
        APIGatewayHttpApiV2ProxyRequest request,
        ILambdaContext context)
    {
        var jobSeekerIdString =
            request.QueryStringParameters?
                .GetValueOrDefault("jobSeekerId");

        if (!int.TryParse(
                jobSeekerIdString,
                out int jobSeekerId) ||
            jobSeekerId <= 0)
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Invalid JobSeekerID.\"}"
            };
        }

        var applications = await _context.JobApplications
            .Where(a => a.JobSeekerID == jobSeekerId)
            .OrderByDescending(a => a.ApplicationDate)
            .Select(a => new
            {
                a.ApplicationID,
                a.JobID,
                a.JobSeekerID,
                a.ApplicationDate,
                a.ResumeURL,
                a.CoverLetter,
                a.Status
            })
            .ToListAsync();

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(new
            {
                success = true,
                count = applications.Count,
                applications
            })
        };
    }
}