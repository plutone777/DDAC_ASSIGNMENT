using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DDAC.Contracts.Employer;
using DDAC.Options;
using DDAC.Services.Employer;
using Xunit;

namespace DDAC.Employer.Tests;

public sealed class InterviewApiEndpointTests
{
    [Theory]
    [InlineData("https://4m5y4le116.execute-api.us-east-1.amazonaws.com")]
    [InlineData("http://127.0.0.1:5123")]
    [InlineData("http://[::1]:5123")]
    public async Task Approved_endpoint_preserves_transport_without_network(string url)
    {
        var key = Guid.NewGuid().ToString("N");
        var date = new DateTime(2030, 1, 2, 11, 20, 0, DateTimeKind.Unspecified);
        using var handler = new Stub(async message =>
        {
            Assert.Equal(HttpMethod.Post, message.Method);
            Assert.Equal(new Uri(url + "/api/interviews/schedule"), message.RequestUri);
            Assert.Equal("2", message.Headers.GetValues("X-Employer-ID").Single());
            Assert.True(message.Headers.GetValues("X-Caller-Key").Single() == key);
            using var json = JsonDocument.Parse(await message.Content!.ReadAsStringAsync());
            Assert.Equal(5, json.RootElement.EnumerateObject().Count());
            Assert.False(json.RootElement.EnumerateObject().Any(p => p.Name.Equals("EmployerID", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(date, json.RootElement.GetProperty("interviewDate").GetDateTime());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ScheduleInterviewResponse
                    { Success = true, InterviewID = 7, ApplicationStatus = "Shortlisted" })
            };
        });
        using var http = new HttpClient(handler);
        var client = new InterviewApiClient(http, Microsoft.Extensions.Options.Options.Create(
            new EmployerInterviewOptions { BaseUrl = url, CallerKey = key }));
        var result = await client.ScheduleAsync(2, new ScheduleInterviewRequest
            { ApplicationID = 1, InterviewDate = date, InterviewType = "Online", Location = null, Notes = null });
        Assert.True(result.Response?.Success);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("http://4m5y4le116.execute-api.us-east-1.amazonaws.com")]
    [InlineData("https://4m5y4le116.execute-api.us-east-1.amazonaws.com:8443")]
    [InlineData("https://another123.execute-api.us-east-1.amazonaws.com")]
    [InlineData("https://4m5y4le116.execute-api.us-east-1.amazonaws.com.evil.example")]
    [InlineData("https://4m5y4le116.execute-api.us-east-1.amazonaws.com/stage")]
    [InlineData("https://4m5y4le116.execute-api.us-east-1.amazonaws.com?key=value")]
    [InlineData("https://4m5y4le116.execute-api.us-east-1.amazonaws.com#fragment")]
    [InlineData("https://user@4m5y4le116.execute-api.us-east-1.amazonaws.com")]
    public async Task Unsafe_endpoint_is_rejected_without_send(string url)
    {
        using var handler = new Stub(_ => throw new InvalidOperationException("No send expected."));
        using var http = new HttpClient(handler);
        var client = new InterviewApiClient(http, Microsoft.Extensions.Options.Options.Create(
            new EmployerInterviewOptions { BaseUrl = url, CallerKey = Guid.NewGuid().ToString("N") }));
        var result = await client.ScheduleAsync(2, new ScheduleInterviewRequest());
        Assert.True(result.Unavailable);
        Assert.Null(result.Response);
        Assert.Equal(0, handler.Calls);
    }

    private sealed class Stub(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        internal int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            return respond(request);
        }
    }
}
