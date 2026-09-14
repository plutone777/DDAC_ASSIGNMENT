using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using DDAC.Data;
using DDAC.JS.JobApplicationLambda.Models;
using DDAC.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace DDAC.JS.JobApplicationLambda;

public class Function
{
    private readonly ApplicationDbContext _context;
    private readonly JobApplicationService _jobApplicationService;

    public Function()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"))
            .Options;

        _context = new ApplicationDbContext(options);
        _jobApplicationService = new JobApplicationService(_context);
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(
        APIGatewayHttpApiV2ProxyRequest request,
        ILambdaContext context)
    {
        var applicationRequest =
            JsonSerializer.Deserialize<JobApplicationRequest>(request.Body);

        if (applicationRequest == null)
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Invalid request body.\"}"
            };
        }

        var result = await _jobApplicationService.SubmitApplicationAsync(
            applicationRequest.JobSeekerID,
            applicationRequest.JobID,
            applicationRequest.CoverLetter);

        if (!result.Success)
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = JsonSerializer.Serialize(new
                {
                    success = false,
                    message = result.Message
                })
            };
        }

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(new
            {
                success = true,
                message = result.Message,
                applicationID = result.Application!.ApplicationID
            })
        };
    }
}