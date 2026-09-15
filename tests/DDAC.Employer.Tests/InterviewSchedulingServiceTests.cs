using System.Net;
using System.Text.RegularExpressions;
using DDAC.Models;
using DDAC.Services.Employer;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace DDAC.Employer.Tests;

public sealed class InterviewSchedulingServiceTests(ITestOutputHelper output)
{
    private void Check(bool ok, string label)
    {
        Assert.True(ok, label);
        output.WriteLine("PASS: " + label);
    }

    [Fact]
    public async Task Validation_and_unchanged_statuses_stop_at_first_failure()
    {
        using var app = new LocalBaselineFixture();
        await app.InitializeAsync();
        output.WriteLine("Fixture: " + app.RunLabel);
        JobInterview Input() => new()
        {
            ApplicationID = app.Submitted.ApplicationID, InterviewDate = DateTime.Now.AddDays(7),
            InterviewType = "On-site", Location = "  LOCAL TEST Room  ", Notes = "  LOCAL TEST Notes  "
        };
        var unchanged = await app.BusinessSnapshotAsync();
        foreach (var field in new[] { "InterviewDate", "InterviewType", "Location", "Binding" })
        {
            var input = Input();
            if (field == "InterviewDate") input.InterviewDate = DateTime.Now.AddDays(-1);
            if (field == "InterviewType") input.InterviewType = "Unsupported";
            if (field == "Location") input.Location = "   ";
            await using var db = app.OpenDb();
            var result = await new InterviewSchedulingService(db).ScheduleAsync(app.Owner.UserID, input, field != "Binding");
            Check(!result.Scheduled && result.Application is not null && (field == "Binding" || result.Errors.ContainsKey(field)), "Service rejects " + field);
        }
        await using (var db = app.OpenDb())
        {
            var result = await new InterviewSchedulingService(db).ScheduleAsync(app.Other.UserID, Input());
            Check(!result.Scheduled && result.Application is null, "Service itself denies cross-owner scheduling");
        }
        Check(unchanged == await app.BusinessSnapshotAsync(), "Rejected service calls leave business records unchanged");

        using var browser = app.Browser();
        static async Task<string> Token(HttpResponseMessage page)
        {
            page.EnsureSuccessStatusCode();
            var m = Regex.Match(await page.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(m.Success, "Expected normal anti-forgery token");
            return WebUtility.HtmlDecode(m.Groups[1].Value);
        }
        using var login = await browser.GetAsync("/User/Login");
        using var signedIn = await browser.PostAsync("/User/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = app.Owner.Email, ["password"] = app.Password, ["__RequestVerificationToken"] = await Token(login)
        }));
        Check(signedIn.StatusCode == HttpStatusCode.Redirect && signedIn.Headers.Location?.OriginalString == "/Employer/Index", "Validation test authenticates normally");
        foreach (var field in new[] { "InterviewDate", "InterviewType", "Location", "MalformedDate" })
        {
            using var form = await browser.GetAsync($"/EmployerApplication/CreateInterview?applicationId={app.Submitted.ApplicationID}");
            var values = new Dictionary<string, string>
            {
                ["ApplicationID"] = app.Submitted.ApplicationID.ToString(),
                ["InterviewDate"] = DateTime.Now.AddDays(7).ToString("yyyy-MM-ddTHH:mm"),
                ["InterviewType"] = "On-site", ["Location"] = "LOCAL TEST Room", ["Notes"] = "LOCAL TEST",
                ["__RequestVerificationToken"] = await Token(form)
            };
            var expected = "";
            if (field == "InterviewDate") { values[field] = DateTime.Now.AddDays(-1).ToString("yyyy-MM-ddTHH:mm"); expected = "Interview date must be in the future."; }
            if (field == "InterviewType") { values[field] = "Unsupported"; expected = "Select a valid interview type."; }
            if (field == "Location") { values[field] = "   "; expected = "Enter a location for an on-site interview."; }
            if (field == "MalformedDate") { values["InterviewDate"] = "not-a-date"; expected = "field-validation-error"; }
            using var response = await browser.PostAsync("/EmployerApplication/CreateInterview", new FormUrlEncodedContent(values));
            Check(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadAsStringAsync()).Contains(expected), "MVC preserves validation feedback for " + field);
        }
        Check(unchanged == await app.BusinessSnapshotAsync(), "Invalid HTTP submissions do not persist interviews/status changes");

        foreach (var status in new[] { "Shortlisted", "Rejected", "Hired" })
        {
            await using (var seed = app.OpenDb())
            {
                (await seed.JobApplications.FindAsync(app.Submitted.ApplicationID))!.Status = status;
                await seed.SaveChangesAsync(); // Only this run's synthetic application.
            }
            var input = Input();
            input.InterviewType = status == "Shortlisted" ? "on-site" : status == "Rejected" ? "online" : "phone";
            if (status != "Shortlisted") { input.Location = null!; input.Notes = null!; }
            await using var db = app.OpenDb();
            var before = await db.JobInterviews.CountAsync(x => x.ApplicationID == input.ApplicationID);
            var result = await new InterviewSchedulingService(db).ScheduleAsync(app.Owner.UserID, input);
            Check(result.Scheduled, "Existing scheduling behavior retained for " + status);
            await using var verify = app.OpenDb();
            Check((await verify.JobApplications.FindAsync(input.ApplicationID))!.Status == status, status + " is not overwritten");
            Check(await verify.JobInterviews.CountAsync(x => x.ApplicationID == input.ApplicationID) == before + 1, "Exactly one interview for " + status);
            var saved = await verify.JobInterviews.SingleAsync(x => x.InterviewID == input.InterviewID);
            var type = status == "Shortlisted" ? "On-site" : status == "Rejected" ? "Online" : "Phone";
            Check(saved.InterviewType == type && saved.Status == "Scheduled" && saved.InterviewDate == input.InterviewDate
                && saved.Location == (status == "Shortlisted" ? "LOCAL TEST Room" : "")
                && saved.Notes == (status == "Shortlisted" ? "LOCAL TEST Notes" : ""), "Normalization, optional fields and date preserved for " + type);
        }
    }
}
