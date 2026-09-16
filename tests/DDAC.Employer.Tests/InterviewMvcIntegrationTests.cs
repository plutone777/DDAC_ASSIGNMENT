using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DDAC.Models;
using DDAC.Services.Employer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace DDAC.Employer.Tests;

public sealed class InterviewMvcIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Mvc_real_api_and_failure_checks_stop_at_first_failure()
    {
        var wire = new WireObserver();
        await using var app = new LocalBaselineFixture { ApiObserver = wire };
        await app.InitializeAsync();
        wire.ExpectedKey = app.CallerKey;
        wire.ExpectedEmployer = app.Owner.UserID.ToString();
        using var browser = app.Browser();
        using var schedulingClient = app.Services.GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(IInterviewApiClient));
        Assert.Equal(TimeSpan.FromSeconds(15), schedulingClient.Timeout);
        void Check(bool ok, string label)
        {
            Assert.True(ok, label);
            output.WriteLine("PASS: " + label);
        }
        async Task<string> Html(HttpResponseMessage response)
        {
            var html = await response.Content.ReadAsStringAsync();
            Check(!html.Contains(wire.ExpectedKey, StringComparison.Ordinal), "Caller key absent from browser output");
            return html;
        }
        static async Task<string> Token(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            var match = Regex.Match(await response.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(match.Success, "Normal MVC form issues CSRF token");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }
        async Task Login(HttpClient client, User user)
        {
            using var form = await client.GetAsync("/User/Login");
            using var result = await client.PostAsync("/User/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = user.Email, ["password"] = app.Password, ["__RequestVerificationToken"] = await Token(form)
            }));
            Check(result.StatusCode == HttpStatusCode.Redirect, "Normal session login succeeds");
            await Html(result);
        }
        await Login(browser, app.Owner);
        var date = DateTime.Now.AddDays(8).Date.AddHours(11).AddMinutes(20);
        async Task<Dictionary<string, string>> Form(int applicationId)
        {
            using var form = await browser.GetAsync($"/EmployerApplication/CreateInterview?applicationId={applicationId}");
            await Html(form);
            return new()
            {
                ["ApplicationID"] = applicationId.ToString(), ["InterviewDate"] = date.ToString("yyyy-MM-ddTHH:mm"),
                ["InterviewType"] = "Online", ["Location"] = "", ["Notes"] = "LOCAL TEST MVC HTTP",
                ["EmployerID"] = app.Other.UserID.ToString(), // Must not override authenticated identity.
                ["__RequestVerificationToken"] = await Token(form)
            };
        }

        var values = await Form(app.Submitted.ApplicationID);
        using (var result = await browser.PostAsync("/EmployerApplication/CreateInterview", new FormUrlEncodedContent(values)))
        {
            Check(result.StatusCode == HttpStatusCode.Redirect && result.Headers.Location?.OriginalString ==
                $"/EmployerApplication/ApplicationDetails/{app.Submitted.ApplicationID}", "Real HTTP scheduling preserves redirect");
            await Html(result);
            using var details = await browser.GetAsync(result.Headers.Location);
            Check((await Html(details)).Contains("Interview scheduled successfully."), "Success message preserved");
        }
        await using var db = app.OpenDb();
        var interviews = await db.JobInterviews.AsNoTracking().Where(x => x.ApplicationID == app.Submitted.ApplicationID).ToListAsync();
        Check(wire.Calls == 1 && wire.LastStatus == HttpStatusCode.OK && interviews.Count == 1,
            "One MVC submission makes one real HTTP request and creates exactly one interview");
        Check(interviews.Single().InterviewID == wire.ReturnedInterviewId, "API InterviewID matches persisted interview");
        Check((await db.JobApplications.AsNoTracking().SingleAsync(x => x.ApplicationID == app.Submitted.ApplicationID)).Status == "Shortlisted",
            "API updates application status");
        Check(interviews.Single().InterviewDate == date, "MVC to JSON to API persistence preserves wall-clock date");
        Check(wire.IdentityMatched && wire.KeyMatched && wire.InputFieldsOnly,
            "Server supplies session identity/key headers; request JSON contains only five business fields");

        var unchanged = await app.BusinessSnapshotAsync();
        var invalid = await Form(app.UnderReview.ApplicationID);
        invalid["InterviewDate"] = DateTime.Now.AddDays(-1).ToString("yyyy-MM-ddTHH:mm");
        using (var result = await browser.PostAsync("/EmployerApplication/CreateInterview", new FormUrlEncodedContent(invalid)))
            Check(result.StatusCode == HttpStatusCode.OK && (await Html(result)).Contains("Interview date must be in the future.")
                && wire.LastStatus == HttpStatusCode.BadRequest, "Real API 400 maps field errors into existing MVC form");
        Check(unchanged == await app.BusinessSnapshotAsync(), "Validation writes no business data");

        using var other = app.Browser();
        await Login(other, app.Other);
        using var tokenPage = await other.GetAsync("/EmployerProfile/EditProfile");
        var denied = await Form(app.UnderReview.ApplicationID);
        denied["__RequestVerificationToken"] = await Token(tokenPage);
        using (var result = await other.PostAsync("/EmployerApplication/CreateInterview", new FormUrlEncodedContent(denied)))
            Check(result.StatusCode == HttpStatusCode.NotFound, "Cross-owner MVC request remains non-disclosing");
        Check(unchanged == await app.BusinessSnapshotAsync(), "Ownership rejection writes no business data");

        var valid = await Form(app.UnderReview.ApplicationID);
        foreach (var token in new[] { "", "LOCAL-TEST-INVALID" })
        {
            var csrf = new Dictionary<string, string>(valid);
            if (token == "") csrf.Remove("__RequestVerificationToken"); else csrf["__RequestVerificationToken"] = token;
            var calls = wire.Calls;
            using var result = await browser.PostAsync("/EmployerApplication/CreateInterview", new FormUrlEncodedContent(csrf));
            Check(result.StatusCode == HttpStatusCode.BadRequest && wire.Calls == calls, "CSRF rejected before API call");
        }

        // Test-only HTTP fault injection. Real typed client/controller remain unchanged.
        foreach (var mode in new[] { "401", "500", "malformed", "unexpected", "timeout" })
        {
            wire.Mode = mode;
            var calls = wire.Calls;
            using var result = await browser.PostAsync("/EmployerApplication/CreateInterview", new FormUrlEncodedContent(valid));
            var html = await Html(result);
            Check(result.StatusCode == HttpStatusCode.OK && html.Contains(mode == "timeout"
                ? "Interview scheduling is temporarily unavailable." : "Unable to confirm interview scheduling."), "Safe MVC feedback for " + mode);
            Check(!html.Contains("LOCAL_INTERNAL_SENTINEL") && !html.Contains("System.InvalidOperationException"), "No raw error disclosure for " + mode);
            Check(wire.Calls == calls + 1, "No automatic retry for " + mode);
            Check(unchanged == await app.BusinessSnapshotAsync(), "No fallback/write for injected " + mode);
        }
        output.WriteLine("Timeout was injected before forwarding: observed database unchanged; remote-commit timeout remains uncertain in general.");
        wire.Mode = "real";
        await app.StopInterviewApiAsync();
        var beforeUnavailable = wire.Calls;
        using (var result = await browser.PostAsync("/EmployerApplication/CreateInterview", new FormUrlEncodedContent(valid)))
            Check(result.StatusCode == HttpStatusCode.OK && (await Html(result)).Contains("Interview scheduling is temporarily unavailable."),
                "Stopped real API produces safe unavailable feedback");
        Check(wire.Calls == beforeUnavailable + 1 && unchanged == await app.BusinessSnapshotAsync(),
            "Stopped API: single attempt, no in-process fallback, no new interview");
        wire.ExpectedKey = "";
    }

    private sealed class WireObserver : DelegatingHandler
    {
        internal string ExpectedKey { get; set; } = "";
        internal string ExpectedEmployer { get; set; } = "";
        internal string Mode { get; set; } = "real";
        internal int Calls { get; private set; }
        internal bool IdentityMatched { get; private set; }
        internal bool KeyMatched { get; private set; }
        internal bool InputFieldsOnly { get; private set; }
        internal int? ReturnedInterviewId { get; private set; }
        internal HttpStatusCode LastStatus { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            IdentityMatched = request.Headers.TryGetValues("X-Employer-ID", out var identity) && identity.Single() == ExpectedEmployer;
            KeyMatched = request.Headers.TryGetValues("X-Caller-Key", out var key) && key.Single() == ExpectedKey;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            InputFieldsOnly = body.RootElement.EnumerateObject().Select(x => x.Name).OrderBy(x => x).SequenceEqual(
                new[] { "applicationID", "interviewDate", "interviewType", "location", "notes" });
            if (Mode == "timeout") await Task.Delay(Timeout.Infinite, cancellationToken);
            if (Mode != "real") return new HttpResponseMessage(Mode == "401" ? HttpStatusCode.Unauthorized :
                Mode == "500" ? HttpStatusCode.InternalServerError : HttpStatusCode.OK)
            {
                Content = new StringContent(Mode == "unexpected" ? "{\"success\":true}" : "LOCAL_INTERNAL_SENTINEL System.InvalidOperationException",
                    Encoding.UTF8, "application/json")
            };
            var response = await base.SendAsync(request, cancellationToken);
            LastStatus = response.StatusCode;
            using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (result.RootElement.TryGetProperty("interviewID", out var id) && id.ValueKind == JsonValueKind.Number)
                ReturnedInterviewId = id.GetInt32();
            return response;
        }
    }
}
