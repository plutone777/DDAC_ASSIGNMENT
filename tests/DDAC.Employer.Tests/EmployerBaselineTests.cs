using System.Net;
using System.Text.RegularExpressions;
using DDAC.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace DDAC.Employer.Tests;

public sealed class EmployerBaselineTests(ITestOutputHelper output)
{
    private const string Warning = "Please sign in with an employer account to continue.";
    private void Check(bool condition, string label)
    {
        Assert.True(condition, label);
        output.WriteLine("PASS: " + label);
    }

    private static async Task<string> TokenAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var match = Regex.Match(await response.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "A normal form must issue an anti-forgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private async Task LoginAsync(HttpClient browser, User user, string password)
    {
        using var page = await browser.GetAsync("/User/Login");
        using var response = await browser.PostAsync("/User/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = user.Email, ["password"] = password, ["__RequestVerificationToken"] = await TokenAsync(page)
        }));
        Check(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.OriginalString == "/Employer/Index", "Normal Employer authentication redirects to dashboard");
    }

    [Fact]
    public async Task Employer_sidebar_uses_shared_antiforgery_logout_and_clears_session()
    {
        using var app = new LocalBaselineFixture(startInterviewApi: false);
        await app.InitializeAsync();
        using var browser = app.Browser();
        await LoginAsync(browser, app.Owner, app.Password);
        using var page = await browser.GetAsync("/Employer/Index");
        var html = await page.Content.ReadAsStringAsync();
        var form = Regex.Match(html, "<form[^>]*action=\"/User/LogoutJS\"[^>]*>.*?</form>", RegexOptions.Singleline);
        Assert.True(form.Success);
        Assert.Contains("method=\"post\"", form.Value);
        Assert.Contains("Logout</button>", form.Value);
        var token = Regex.Match(form.Value, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        using var denied = await browser.PostAsync("/User/LogoutJS", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        using var logout = await browser.PostAsync("/User/LogoutJS", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
        }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.True(logout.Headers.Location?.OriginalString is "/" or "/User/Login");
        using var dashboard = await browser.GetAsync("/Employer/Index");
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
        Assert.True(dashboard.Headers.Location?.OriginalString is "/" or "/User/Login");
    }

    // One ordered Fact deliberately stops the entire baseline at its first failure.
    [Fact]
    public async Task Local_only_baseline_stops_at_first_failure()
    {
        using var app = new LocalBaselineFixture();
        await app.InitializeAsync();
        output.WriteLine("Fixture: " + app.RunLabel);
        using var owner = app.Browser();
        using var denied = await owner.GetAsync("/Employer/Index");
        Check(denied.StatusCode == HttpStatusCode.Redirect && denied.Headers.Location?.OriginalString is "/" or "/User/Login", "Anonymous Employer access redirects to Login");
        using var login = await owner.GetAsync(denied.Headers.Location);
        Check((await login.Content.ReadAsStringAsync()).Contains(Warning), "Warning appears on Login");
        using var authenticated = await owner.PostAsync("/User/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = app.Owner.Email, ["password"] = app.Password, ["__RequestVerificationToken"] = await TokenAsync(login)
        }));
        Check(authenticated.StatusCode == HttpStatusCode.Redirect && authenticated.Headers.Location?.OriginalString == "/Employer/Index", "Login succeeds after warning");
        using var dashboard = await owner.GetAsync("/Employer/Index");
        var dashboardHtml = await dashboard.Content.ReadAsStringAsync();
        Check(dashboard.StatusCode == HttpStatusCode.OK && dashboardHtml.Contains(app.Owner.FullName) && !dashboardHtml.Contains(Warning), "Dashboard has authenticated identity and no stale warning");

        foreach (var application in new[] { app.Submitted, app.UnderReview })
        {
            await using var beforeDb = app.OpenDb();
            var before = await beforeDb.JobInterviews.CountAsync(x => x.ApplicationID == application.ApplicationID);
            using var form = await owner.GetAsync($"/EmployerApplication/CreateInterview?applicationId={application.ApplicationID}");
            var when = DateTime.Now.AddDays(7).ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture);
            using var result = await owner.PostAsync("/EmployerApplication/CreateInterview", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ApplicationID"] = application.ApplicationID.ToString(), ["InterviewDate"] = when,
                ["InterviewType"] = "On-site", ["Location"] = app.RunLabel, ["Notes"] = app.RunLabel,
                ["__RequestVerificationToken"] = await TokenAsync(form)
            }));
            Check(result.StatusCode == HttpStatusCode.Redirect, $"Scheduling from {application.Status} redirects successfully");
            await using var afterDb = app.OpenDb();
            var interviews = await afterDb.JobInterviews.Where(x => x.ApplicationID == application.ApplicationID).ToListAsync();
            Check(interviews.Count == before + 1, $"Exactly one interview added from {application.Status}");
            Check(interviews.Single().Status == "Scheduled" && interviews.Single().Location == app.RunLabel && interviews.Single().InterviewType == "On-site", "Saved interview fields match submission");
            Check((await afterDb.JobApplications.FindAsync(application.ApplicationID))!.Status == "Shortlisted", $"{application.Status} becomes Shortlisted");
            using var details = await owner.GetAsync(result.Headers.Location);
            var html = await details.Content.ReadAsStringAsync();
            Check(details.StatusCode == HttpStatusCode.OK && html.Contains("Interview scheduled successfully.") && html.Contains("Shortlisted"), "Scheduling result renders on application details");
        }

        using var other = app.Browser();
        await LoginAsync(other, app.Other, app.Password);
        var unchanged = await app.BusinessSnapshotAsync();
        foreach (var path in new[]
        {
            $"/JobVacancy/VacancyDetails/{app.Vacancy.JobID}", $"/JobVacancy/EditVacancy/{app.Vacancy.JobID}",
            $"/EmployerApplication/ApplicationDetails/{app.Submitted.ApplicationID}",
            $"/EmployerApplication/CreateInterview?applicationId={app.Submitted.ApplicationID}",
            $"/EmployerInquiry/InquiryDetails/{app.Inquiry.InquiryID}"
        })
        {
            using var response = await other.GetAsync(path);
            Check(response.StatusCode == HttpStatusCode.NotFound, "Cross-owner GET denied: " + path);
        }
        using var otherForm = await other.GetAsync("/JobVacancy/CreateVacancy");
        var otherToken = await TokenAsync(otherForm);
        var vacancy = new Dictionary<string, string>
        {
            ["JobTitle"] = app.RunLabel + " attempt", ["Description"] = "LOCAL TEST", ["Location"] = "LOCAL TEST",
            ["EmploymentType"] = "Full-time", ["Salary"] = "1000", ["Status"] = "Draft",
            ["ClosingDate"] = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),
            ["AccessibilityFeatures"] = "LOCAL TEST", ["AccommodationsAvailable"] = "LOCAL TEST"
        };
        var interview = new Dictionary<string, string>
        {
            ["ApplicationID"] = app.Submitted.ApplicationID.ToString(), ["InterviewDate"] = DateTime.Now.AddDays(8).ToString("yyyy-MM-ddTHH:mm"),
            ["InterviewType"] = "On-site", ["Location"] = app.RunLabel, ["Notes"] = "LOCAL TEST"
        };
        var status = new Dictionary<string, string> { ["id"] = app.Submitted.ApplicationID.ToString(), ["status"] = "Rejected" };
        var ownerPosts = new[]
        {
            (Path: $"/JobVacancy/EditVacancy/{app.Vacancy.JobID}", Body: vacancy),
            (Path: "/EmployerApplication/UpdateStatus", Body: status),
            (Path: "/EmployerApplication/CreateInterview", Body: interview)
        };
        foreach (var test in ownerPosts)
        {
            var body = new Dictionary<string, string>(test.Body) { ["__RequestVerificationToken"] = otherToken };
            using var response = await other.PostAsync(test.Path, new FormUrlEncodedContent(body));
            Check(response.StatusCode == HttpStatusCode.NotFound, "Cross-owner POST denied with valid CSRF: " + test.Path);
        }
        Check(unchanged == await app.BusinessSnapshotAsync(), "Ownership denials do not mutate fixture business data");

        using var csrfPage = await owner.GetAsync("/EmployerProfile/EditProfile");
        _ = await TokenAsync(csrfPage); // Real authenticated browser has its normal anti-forgery cookie.
        var posts = ownerPosts.Concat(new[]
        {
            (Path: "/JobVacancy/CreateVacancy", Body: vacancy),
            (Path: "/EmployerProfile/EditProfile", Body: new Dictionary<string, string>
            {
                ["CompanyName"] = app.RunLabel + " attempt", ["Industry"] = "Testing", ["CompanyDescription"] = "LOCAL TEST",
                ["Address"] = "LOCAL TEST", ["Website"] = "https://example.invalid"
            }),
            (Path: "/EmployerInquiry/CreateInquiry", Body: new Dictionary<string, string>
            {
                ["AdvisorID"] = app.Advisor.UserID.ToString(), ["Subject"] = app.RunLabel, ["Message"] = "LOCAL TEST"
            })
        });
        foreach (var test in posts)
        foreach (var invalid in new[] { false, true })
        {
            var body = new Dictionary<string, string>(test.Body);
            if (invalid) body["__RequestVerificationToken"] = "LOCAL-TEST-INVALID-TOKEN";
            using var response = await owner.PostAsync(test.Path, new FormUrlEncodedContent(body));
            Check(response.StatusCode == HttpStatusCode.BadRequest, $"{(invalid ? "Invalid" : "Missing")} CSRF rejected: {test.Path}");
        }
        Check(unchanged == await app.BusinessSnapshotAsync(), "CSRF denials do not mutate fixture business data");
        output.WriteLine("All checkpoints passed. Fixtures retained locally; no schema changes or automatic cleanup.");
    }
}
