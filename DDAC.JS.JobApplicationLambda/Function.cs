using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;
using DDAC.JS.JobApplicationLambda.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

[assembly: LambdaSerializer(
    typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace DDAC.JS.JobApplicationLambda;

public class Function
{
    private readonly IAmazonSQS _sqsClient;
    private readonly string _queueUrl;

    public Function()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        _sqsClient = new AmazonSQSClient();

        _queueUrl = configuration["SQS_QUEUE_URL"]
            ?? throw new Exception("SQS_QUEUE_URL is not configured.");
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

        var messageBody =
            JsonSerializer.Serialize(applicationRequest);

        var sendRequest = new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = messageBody
        };

        await _sqsClient.SendMessageAsync(sendRequest);

        context.Logger.LogLine(
            "Job application message sent to SQS.");

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 202,
            Body = JsonSerializer.Serialize(new
            {
                success = true,
                message = "Job application submitted for processing."
            })
        };
    }
}