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
        context.Logger.LogLine(
            $"RAW REQUEST BODY: {request.Body}");

        var guidance =
            JsonSerializer.Deserialize<CareerGuidance>(
                request.Body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (guidance == null)
        {
            context.Logger.LogLine(
                "GUIDANCE DESERIALIZATION FAILED.");

            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Invalid request body.\"}"
            };
        }

        context.Logger.LogLine(
            $"GUIDANCE RECEIVED: JobSeekerID={guidance.JobSeekerID}, " +
            $"AdvisorID={guidance.AdvisorID}, " +
            $"GuidanceType={guidance.GuidanceType}, " +
            $"Subject={guidance.Subject}");

        if (guidance.JobSeekerID <= 0)
        {
            context.Logger.LogLine("INVALID JOBSEEKER ID.");

            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Invalid JobSeekerID.\"}"
            };
        }

        if (guidance.AdvisorID <= 0)
        {
            context.Logger.LogLine("INVALID ADVISOR ID.");

            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Invalid AdvisorID.\"}"
            };
        }

        if (string.IsNullOrWhiteSpace(guidance.GuidanceType))
        {
            context.Logger.LogLine("GUIDANCE TYPE IS EMPTY.");

            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Guidance type is required.\"}"
            };
        }

        if (string.IsNullOrWhiteSpace(guidance.Subject))
        {
            context.Logger.LogLine("SUBJECT IS EMPTY.");

            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Subject is required.\"}"
            };
        }

        if (string.IsNullOrWhiteSpace(guidance.GuidanceNotes))
        {
            context.Logger.LogLine("GUIDANCE NOTES ARE EMPTY.");

            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Guidance notes are required.\"}"
            };
        }

        context.Logger.LogLine(
            "VALIDATION PASSED. SAVING GUIDANCE TO RDS.");

        guidance.GuidanceDate = DateTime.Now;
        guidance.Status = "Requested";

        _context.CareerGuidances.Add(guidance);

        try
        {
            await _context.SaveChangesAsync();

            context.Logger.LogLine(
                $"GUIDANCE SAVED: GuidanceID={guidance.GuidanceID}, " +
                $"JobSeekerID={guidance.JobSeekerID}, " +
                $"AdvisorID={guidance.AdvisorID}");
        }
        catch (Exception ex)
        {
            context.Logger.LogLine(
                $"GUIDANCE SAVE ERROR: {ex}");

            throw;
        }

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