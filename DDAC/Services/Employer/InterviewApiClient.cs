using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DDAC.Contracts.Employer;
using DDAC.Options;
using Microsoft.Extensions.Options;

namespace DDAC.Services.Employer;

public sealed class InterviewApiClient(HttpClient http, IOptions<EmployerInterviewOptions> settings) : IInterviewApiClient
{
    public async Task<InterviewApiResult> ScheduleAsync(int trustedEmployerId, ScheduleInterviewRequest request)
    {
        var options = settings.Value;
        // Explicit local testing or the verified HTTPS API Gateway host; no redirects or fallback.
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var address) ||
            !IsAllowedEndpoint(address) ||
            address.AbsolutePath != "/" || address.Query.Length != 0 || address.Fragment.Length != 0 ||
            address.UserInfo.Length != 0 || string.IsNullOrWhiteSpace(options.CallerKey) || options.CallerKey.Length < 32 ||
            options.CallerKey.Any(char.IsControl) || trustedEmployerId <= 0)
            return new(null, true);
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(address, "/api/interviews/schedule"))
                { Content = JsonContent.Create(request) };
            message.Headers.Add("X-Caller-Key", options.CallerKey);
            message.Headers.Add("X-Employer-ID", trustedEmployerId.ToString(CultureInfo.InvariantCulture));
            // One send only. DI disables redirects and adds no retry handlers.
            using var response = await http.SendAsync(message);
            if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.BadRequest or HttpStatusCode.NotFound))
                return new(null);
            var body = await response.Content.ReadFromJsonAsync<ScheduleInterviewResponse>();
            if (body is null || body.Errors is null) return new(null);
            return response.StatusCode switch
            {
                HttpStatusCode.OK when body.Success && body.InterviewID > 0 && !string.IsNullOrWhiteSpace(body.ApplicationStatus)
                    && body.ErrorCode is null && body.Errors.Count == 0 => new(body),
                HttpStatusCode.BadRequest when !body.Success && body.ErrorCode == ScheduleInterviewResponse.ValidationFailed
                    && body.Errors.Count > 0 && body.Errors.All(error => error.Value is not null && error.Value.Length > 0
                        && error.Value.All(message => !string.IsNullOrWhiteSpace(message))) => new(body),
                HttpStatusCode.NotFound when !body.Success && body.ErrorCode == ScheduleInterviewResponse.NotFound => new(body),
                _ => new(null)
            };
        }
        catch (HttpRequestException) { return new(null, true); }
        catch (OperationCanceledException) { return new(null, true); }
        catch (JsonException) { return new(null); }
        catch (NotSupportedException) { return new(null); }
    }

    private static bool IsAllowedEndpoint(Uri address) =>
        (address.Host is "127.0.0.1" or "[::1]" && address.Scheme is "http" or "https") ||
        (address.Scheme == "https" && address.Port == 443 &&
         address.Host == "4m5y4le116.execute-api.us-east-1.amazonaws.com");
}
