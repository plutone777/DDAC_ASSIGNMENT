using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using DDAC.JS.JobApplicationProcessorLambda.Data;
using DDAC.JS.JobApplicationProcessorLambda.Models;
using DDAC.JS.JobApplicationProcessorLambda.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

[assembly: LambdaSerializer(
    typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace DDAC.JS.JobApplicationProcessorLambda;

public class Function
{
    private readonly ApplicationDbContext _context;
    private readonly JobApplicationService _jobApplicationService;

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
        _jobApplicationService = new JobApplicationService(_context);
    }

    public async Task FunctionHandler(
        SQSEvent sqsEvent,
        ILambdaContext context)
    {
        foreach (var record in sqsEvent.Records)
        {
            try
            {
                var applicationRequest =
                    JsonSerializer.Deserialize<JobApplicationRequest>(
                        record.Body);

                if (applicationRequest == null)
                {
                    context.Logger.LogLine(
                        "Invalid SQS message body.");
                    continue;
                }

                var result =
                    await _jobApplicationService.SubmitApplicationAsync(
                        applicationRequest.JobSeekerID,
                        applicationRequest.JobID,
                        applicationRequest.CoverLetter);

                context.Logger.LogLine(
                    $"Application processing result: {result.Message}");
            }
            catch (Exception ex)
            {
                context.Logger.LogLine(
                    $"Error processing SQS message: {ex.Message}");

                throw;
            }
        }
    }
}