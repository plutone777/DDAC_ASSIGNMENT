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
        var inquiry =
            JsonSerializer.Deserialize<Inquiry>(
                request.Body);

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

        _context.Inquiries.Add(inquiry);

        await _context.SaveChangesAsync();

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