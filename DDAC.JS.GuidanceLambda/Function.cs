using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using DDAC.Data;
using DDAC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

[assembly: LambdaSerializer(
    typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer)
)]

namespace DDAC.JS.GuidanceLambda;

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
        var guidance =
            JsonSerializer.Deserialize<CareerGuidance>(
                request.Body);

        if (guidance == null)
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Invalid request body.\"}"
            };
        }

        if (guidance.JobSeekerID <= 0)
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Invalid JobSeekerID.\"}"
            };
        }

        if (guidance.AdvisorID <= 0)
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Invalid AdvisorID.\"}"
            };
        }

        if (string.IsNullOrWhiteSpace(guidance.GuidanceType))
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Guidance type is required.\"}"
            };
        }

        if (string.IsNullOrWhiteSpace(guidance.Subject))
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Subject is required.\"}"
            };
        }

        if (string.IsNullOrWhiteSpace(guidance.GuidanceNotes))
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Guidance notes are required.\"}"
            };
        }

        guidance.GuidanceDate = DateTime.Now;
        guidance.Status = "Requested";

        _context.CareerGuidances.Add(guidance);

        await _context.SaveChangesAsync();

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(new
            {
                success = true,
                message =
                    "Your career guidance request has been submitted successfully.",
                guidanceID = guidance.GuidanceID
            })
        };
    }
}