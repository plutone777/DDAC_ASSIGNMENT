using Amazon.Lambda.APIGatewayEvents;
using DDAC.Contracts.Employer;
using DDAC.Employer.Interviews.Lambda;
using DDAC.Models;
using DDAC.Services.Employer;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace DDAC.Employer.Lambda.Tests;

public sealed class InterviewLambdaTests
{
    private readonly string key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static readonly DateTime Date = new(2035, 5, 6, 14, 30, 0, DateTimeKind.Unspecified);

    private APIGatewayHttpApiV2ProxyRequest Request() => new()
    {
        Headers = new Dictionary<string, string> { ["X-Caller-Key"] = key, ["X-Employer-ID"] = "17" },
        Body = JsonSerializer.Serialize(new ScheduleInterviewRequest
        {
            ApplicationID = 23, InterviewDate = Date, InterviewType = "Online", Location = null, Notes = null
        })
    };

    private Function Handler(FakeInvocation fake) => new(() => key, () => fake);
    private static ScheduleInterviewResponse Read(APIGatewayHttpApiV2ProxyResponse response) =>
        JsonSerializer.Deserialize<ScheduleInterviewResponse>(response.Body)!;

    [Fact]
    public async Task Success_maps_request_once_preserves_date_and_disposes_scope()
    {
        var fake = new FakeInvocation();
        var request = Request();
        using var json = JsonDocument.Parse(request.Body);
        Assert.Equal(new[] { "applicationID", "interviewDate", "interviewType", "location", "notes" },
            json.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        var response = await Handler(fake).FunctionHandler(request, null!);
        Assert.Equal(200, response.StatusCode);
        var result = Read(response);
        Assert.True(result.Success);
        Assert.Equal(91, result.InterviewID);
        Assert.Equal("Shortlisted", result.ApplicationStatus);
        Assert.Equal(1, fake.Calls);
        Assert.Equal(17, fake.EmployerId);
        Assert.Equal(23, fake.Input!.ApplicationID);
        Assert.Equal(Date, fake.Input.InterviewDate);
        Assert.Equal(DateTimeKind.Unspecified, fake.Input.InterviewDate.Kind);
        Assert.Null(fake.Input.Location);
        Assert.Null(fake.Input.Notes);
        Assert.True(fake.Disposed);
        Assert.DoesNotContain(key, response.Body);
        using var output = JsonDocument.Parse(response.Body);
        Assert.Equal(new[] { "success", "interviewID", "applicationStatus", "errorCode", "errors" },
            output.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
    }

    [Fact]
    public async Task Json_identity_cannot_override_authenticated_header()
    {
        var fake = new FakeInvocation();
        var request = Request();
        request.Body = request.Body[..^1] + ",\"EmployerID\":999}";
        Assert.Equal(200, (await Handler(fake).FunctionHandler(request, null!)).StatusCode);
        Assert.Equal(17, fake.EmployerId);
    }

    [Theory]
    [InlineData("missing-key")]
    [InlineData("invalid-key")]
    [InlineData("missing-id")]
    [InlineData("invalid-id")]
    [InlineData("duplicate-key")]
    public async Task Untrusted_headers_rejected_before_database_scope(string mode)
    {
        var request = Request();
        if (mode == "missing-key") request.Headers.Remove("X-Caller-Key");
        if (mode == "invalid-key") request.Headers["X-Caller-Key"] = Guid.NewGuid().ToString();
        if (mode == "missing-id") request.Headers.Remove("X-Employer-ID");
        if (mode == "invalid-id") request.Headers["X-Employer-ID"] = "-1";
        if (mode == "duplicate-key") request.Headers["x-caller-key"] = key;
        var created = 0;
        var handler = new Function(() => key, () => { created++; return new FakeInvocation(); });
        var response = await handler.FunctionHandler(request, null!);
        Assert.Equal(401, response.StatusCode);
        Assert.Equal("unauthorized", Read(response).ErrorCode);
        Assert.Equal(0, created);
    }

    [Fact]
    public async Task Inactive_or_wrong_role_identity_rejected()
    {
        var fake = new FakeInvocation { Active = false };
        Assert.Equal(401, (await Handler(fake).FunctionHandler(Request(), null!)).StatusCode);
        Assert.Equal(0, fake.Calls);
        Assert.True(fake.Disposed);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"applicationID\":0,\"interviewDate\":\"bad\"}")]
    public async Task Invalid_contract_is_structured_400_without_service_call(string body)
    {
        var fake = new FakeInvocation();
        var request = Request(); request.Body = body;
        var response = await Handler(fake).FunctionHandler(request, null!);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("validation_failed", Read(response).ErrorCode);
        Assert.NotEmpty(Read(response).Errors);
        Assert.Equal(0, fake.Calls);
    }

    [Theory]
    [InlineData("InterviewDate")]
    [InlineData("InterviewType")]
    [InlineData("Location")]
    public async Task Business_validation_maps_service_errors(string field)
    {
        var fake = new FakeInvocation { ErrorField = field };
        var response = await Handler(fake).FunctionHandler(Request(), null!);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains(JsonNamingPolicy.CamelCase.ConvertName(field), Read(response).Errors.Keys);
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task Missing_and_foreign_application_have_identical_not_found_response()
    {
        var missing = new FakeInvocation { NotFound = true };
        var foreign = new FakeInvocation { NotFound = true };
        var first = await Handler(missing).FunctionHandler(Request(), null!);
        var second = await Handler(foreign).FunctionHandler(Request(), null!);
        Assert.Equal(404, first.StatusCode);
        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(first.Body, second.Body);
        Assert.Equal("not_found", Read(first).ErrorCode);
    }

    [Fact]
    public async Task Unexpected_service_failure_is_safe_and_not_retried()
    {
        var fake = new FakeInvocation { Throw = true };
        var response = await Handler(fake).FunctionHandler(Request(), null!);
        Assert.Equal(500, response.StatusCode);
        Assert.Equal("internal_error", Read(response).ErrorCode);
        Assert.DoesNotContain(fake.ErrorMarker, response.Body);
        Assert.DoesNotContain(key, response.Body);
        Assert.Equal(1, fake.Calls);
        Assert.True(fake.Disposed);
    }

    [Fact]
    public async Task Configuration_and_scope_failures_are_safe()
    {
        var missingKey = new Function(() => null, () => throw new InvalidOperationException());
        Assert.Equal(500, (await missingKey.FunctionHandler(Request(), null!)).StatusCode);
        var marker = Guid.NewGuid().ToString();
        var invalidDatabase = new Function(() => key, () => throw new InvalidOperationException(marker));
        var result = await invalidDatabase.FunctionHandler(Request(), null!);
        Assert.Equal(500, result.StatusCode);
        Assert.DoesNotContain(marker, result.Body);
    }

    [Fact]
    public async Task Base64_event_and_case_insensitive_headers_are_supported()
    {
        var fake = new FakeInvocation();
        var request = Request();
        request.Headers = new Dictionary<string, string> { ["x-caller-key"] = key, ["x-employer-id"] = "17" };
        request.IsBase64Encoded = true;
        request.Body = Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Body));
        Assert.Equal(200, (await Handler(fake).FunctionHandler(request, null!)).StatusCode);
        Assert.Equal(Date, fake.Input!.InterviewDate);
    }

    [Fact]
    public async Task Each_invocation_gets_a_new_disposed_scope()
    {
        var scopes = new List<FakeInvocation>();
        var handler = new Function(() => key, () => { var scope = new FakeInvocation(); scopes.Add(scope); return scope; });
        await handler.FunctionHandler(Request(), null!);
        await handler.FunctionHandler(Request(), null!);
        Assert.Equal(2, scopes.Count);
        Assert.NotSame(scopes[0], scopes[1]);
        Assert.All(scopes, scope => { Assert.True(scope.Disposed); Assert.Equal(1, scope.Calls); });
    }

    private sealed class FakeInvocation : ISchedulingInvocation, IInterviewSchedulingService
    {
        public bool Active = true, NotFound, Throw, Disposed;
        public string? ErrorField;
        public readonly string ErrorMarker = Guid.NewGuid().ToString();
        public int Calls, EmployerId;
        public JobInterview? Input;
        public IInterviewSchedulingService Service => this;
        public IReadOnlyList<string> InterviewTypes => new[] { "On-site", "Online", "Phone" };
        public Task<bool> IsActiveEmployerAsync(int employerId) => Task.FromResult(Active);
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
        public Task<InterviewSchedulingResult> ScheduleAsync(int employerId, JobInterview input, bool inputIsValid = true)
        {
            Calls++; EmployerId = employerId; Input = input;
            if (Throw) throw new InvalidOperationException(ErrorMarker);
            if (NotFound) return Task.FromResult(new InterviewSchedulingResult(null, null, new Dictionary<string, string>(), false));
            var errors = new Dictionary<string, string>();
            if (ErrorField is not null) errors[ErrorField] = "Invalid value.";
            input.InterviewID = 91;
            return Task.FromResult(new InterviewSchedulingResult(new JobApplication { Status = "Shortlisted" },
                new JobVacancy(), errors, ErrorField is null));
        }
    }
}
