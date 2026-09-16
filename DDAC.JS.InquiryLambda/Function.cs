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

namespace DDAC.JS.InquiryLambda;

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
        context.Logger.LogLine($"RAW REQUEST BODY: {request.Body}");

        var inquiry =
            JsonSerializer.Deserialize<Inquiry>(
                request.Body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (inquiry == null)
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Invalid request body.\"}"
            };
        }

        if (inquiry.UserID <= 0)
        {
            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 400,
                Body = "{\"message\":\"Invalid UserID.\"}"
            };
        }

        inquiry.Status = "Open";
        inquiry.CreatedDate = DateTime.Now;

        context.Logger.LogLine("BEFORE ADD");

        _context.Inquiries.Add(inquiry);

        context.Logger.LogLine("BEFORE SAVE");

        try
        {
            await _context.SaveChangesAsync();

            context.Logger.LogLine("AFTER SAVE");

            context.Logger.LogLine(
                $"INQUIRY SAVED: InquiryID={inquiry.InquiryID}, UserID={inquiry.UserID}, AdvisorID={inquiry.AdvisorID}");
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"SAVE ERROR: {ex}");
            throw;
        }

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(new
            {
                success = true,
                message = "Your inquiry has been submitted successfully.",
                inquiryID = inquiry.InquiryID
            })
        };
    }
}
