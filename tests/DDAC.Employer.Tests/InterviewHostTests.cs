using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DDAC.Contracts.Employer;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace DDAC.Employer.Tests;

public sealed class InterviewHostTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Separate_local_host_checks_stop_at_first_failure()
    {
        await using var fixture = new LocalBaselineFixture(startInterviewApi: false);
        await fixture.InitializeAsync();
        await using var host = await RunningHost.StartAsync();
        void Check(bool condition, string name)
        {
            Assert.True(condition, name);
            output.WriteLine("PASS: " + name);
        }
        var request = new ScheduleInterviewRequest
        {
            ApplicationID = fixture.Submitted.ApplicationID,
            InterviewDate = DateTime.SpecifyKind(DateTime.Now.AddDays(4).Date.AddHours(10), DateTimeKind.Unspecified),
            InterviewType = "online", Location = null, Notes = null
        };
        async Task<(HttpStatusCode Status, ScheduleInterviewResponse Body, string Json)> Send(
            ScheduleInterviewRequest input, string? key, int? employerId, string? raw = null)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/interviews/schedule")
            { Content = raw is null ? JsonContent.Create(input) : new StringContent(raw, Encoding.UTF8, "application/json") };
            if (key is not null) message.Headers.Add("X-Caller-Key", key);
            if (employerId.HasValue) message.Headers.Add("X-Employer-ID", employerId.Value.ToString());
            using var response = await host.Client.SendAsync(message);
            var json = await response.Content.ReadAsStringAsync();
            var body = JsonSerializer.Deserialize<ScheduleInterviewResponse>(json);
            Check(body is not null, "Response is the safe transport contract");
            return (response.StatusCode, body!, json);
        }

        var before = await fixture.BusinessSnapshotAsync();
        var missingKey = await Send(request, null, fixture.Owner.UserID);
        Check(missingKey.Status == HttpStatusCode.Unauthorized, "Missing caller key rejected");
        var invalidKey = await Send(request, Guid.NewGuid().ToString("N"), fixture.Owner.UserID);
        Check(invalidKey.Status == HttpStatusCode.Unauthorized, "Invalid caller key rejected");
        var missingIdentity = await Send(request, host.Key, null);
        Check(missingIdentity.Status == HttpStatusCode.Unauthorized, "Missing separate Employer identity rejected");
        var wrongRole = await Send(request, host.Key, fixture.Advisor.UserID);
        Check(wrongRole.Status == HttpStatusCode.Unauthorized, "Non-Employer identity rejected");
        Check(before == await fixture.BusinessSnapshotAsync(), "Authentication rejection makes no business writes");

        // Even a valid key plus a spoofed JSON EmployerID cannot override header identity.
        var spoofed = JsonSerializer.Serialize(request)[..^1] + ",\"employerID\":" + fixture.Owner.UserID + "}";
        var denied = await Send(request, host.Key, fixture.Other.UserID, spoofed);
        var absent = await Send(request with { ApplicationID = int.MaxValue }, host.Key, fixture.Owner.UserID);
        Check(denied.Status == HttpStatusCode.NotFound && absent.Status == HttpStatusCode.NotFound,
            "Ownership denial and missing application both return 404");
        Check(denied.Json == absent.Json && denied.Body.ErrorCode == "not_found",
            "Ownership denial and missing application are indistinguishable");
        Check(before == await fixture.BusinessSnapshotAsync(), "Ownership/not-found rejection makes no business writes");

        foreach (var invalid in new[]
        {
            (Input: request with { InterviewType = "invalid" }, Field: "interviewType"),
            (Input: request with { InterviewDate = DateTime.Now.AddDays(-1) }, Field: "interviewDate"),
            (Input: request with { InterviewType = "On-site", Location = " " }, Field: "location"),
            (Input: request with { InterviewDate = null }, Field: "interviewDate")
        })
        {
            var result = await Send(invalid.Input, host.Key, fixture.Owner.UserID);
            Check(result.Status == HttpStatusCode.BadRequest && result.Body.ErrorCode == "validation_failed"
                && result.Body.Errors.ContainsKey(invalid.Field), "Structured validation: " + invalid.Field);
        }
        var malformed = await Send(request, host.Key, fixture.Owner.UserID, "{\"interviewDate\":\"invalid\"}");
        Check(malformed.Status == HttpStatusCode.BadRequest && malformed.Body.ErrorCode == "validation_failed",
            "Malformed date returns safe validation response");
        Check(before == await fixture.BusinessSnapshotAsync(), "Validation rejection makes no business writes");

        await using var db = fixture.OpenDb();
        var initialCount = await db.JobInterviews.CountAsync(x => x.ApplicationID == request.ApplicationID);
        var success = await Send(request, host.Key, fixture.Owner.UserID);
        Check(success.Status == HttpStatusCode.OK && success.Body.Success, "Authenticated owner schedules via independent host");
        Check(await db.JobInterviews.CountAsync(x => x.ApplicationID == request.ApplicationID) == initialCount + 1,
            "Exactly one interview created by the request");
        var saved = await db.JobInterviews.AsNoTracking().SingleAsync(x => x.InterviewID == success.Body.InterviewID);
        Check(saved.ApplicationID == request.ApplicationID && saved.InterviewID == success.Body.InterviewID,
            "Returned InterviewID identifies the saved interview");
        Check(success.Body.ApplicationStatus == "Shortlisted" &&
            (await db.JobApplications.AsNoTracking().SingleAsync(x => x.ApplicationID == request.ApplicationID)).Status == "Shortlisted",
            "Submitted application is Shortlisted in response and database");
        Check(saved.InterviewDate == request.InterviewDate, "Wall-clock interview date survives HTTP without conversion");
        Check(saved.InterviewType == "Online" && saved.Location == "" && saved.Notes == "" && saved.Status == "Scheduled",
            "Existing normalization and optional Location/Notes behavior preserved");

        using var document = JsonDocument.Parse(success.Json);
        Check(document.RootElement.EnumerateObject().Select(x => x.Name).SequenceEqual(
            new[] { "success", "interviewID", "applicationStatus", "errorCode", "errors" }),
            "Success exposes only approved response fields");
        Check(!success.Json.Contains(host.Key, StringComparison.Ordinal) && !denied.Json.Contains(host.Key, StringComparison.Ordinal)
            && !malformed.Json.Contains("Exception", StringComparison.Ordinal), "No secret or exception details in checked responses");

        var onsite = request with
        {
            ApplicationID = fixture.UnderReview.ApplicationID,
            InterviewType = "on-site", Location = "  LOCAL TEST Office  ", Notes = "  LOCAL TEST preparations  "
        };
        var onsiteResult = await Send(onsite, host.Key, fixture.Owner.UserID);
        Check(onsiteResult.Status == HttpStatusCode.OK && onsiteResult.Body.ApplicationStatus == "Shortlisted",
            "Under Review application schedules and becomes Shortlisted");
        var onsiteSaved = await db.JobInterviews.AsNoTracking().SingleAsync(x => x.InterviewID == onsiteResult.Body.InterviewID);
        Check(onsiteSaved.Location == "LOCAL TEST Office" && onsiteSaved.Notes == "LOCAL TEST preparations"
            && await db.JobInterviews.CountAsync(x => x.ApplicationID == onsite.ApplicationID) == 1,
            "On-site fields trimmed with exactly one interview");
    }

    internal sealed class RunningHost : IAsyncDisposable
    {
        internal required HttpClient Client { get; init; }
        internal required string Key { get; init; }
        private Process Process { get; init; } = null!;
        private Task<string> Stdout { get; init; } = null!;
        private Task<string> Stderr { get; init; } = null!;

        internal static async Task<RunningHost> StartAsync()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DDAC.slnx"))) directory = directory.Parent;
            if (directory is null) throw new InvalidOperationException("Solution directory unavailable.");
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            var project = Path.Combine(directory.FullName, "DDAC.Employer.Interviews.LocalApi");
            var assembly = Path.Combine(project, "bin", configuration, "net10.0", "DDAC.Employer.Interviews.LocalApi.dll");
            if (!File.Exists(assembly)) throw new InvalidOperationException("Build the local API before running its tests.");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            var key = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = project, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(assembly);
            start.Environment["DDAC_INTERVIEWS_CALLER_KEY"] = key;
            start.Environment["DDAC_INTERVIEWS_PORT"] = port.ToString();
            var process = Process.Start(start) ?? throw new InvalidOperationException("Could not launch local API.");
            var host = new RunningHost
            {
                Process = process, Key = key, Stdout = process.StandardOutput.ReadToEndAsync(), Stderr = process.StandardError.ReadToEndAsync(),
                Client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(5) }
            };
            try
            {
                for (var attempt = 0; attempt < 100; attempt++)
                {
                    if (process.HasExited)
                    {
                        var diagnostic = System.Text.RegularExpressions.Regex.Matches(await host.Stderr,
                            @"(?m)^(?:StartupPhase: (?:CallerConfigurationValidation|ConfigurationLoading|ConfigurationSources|LoggingConfiguration|UrlConfiguration|ServiceRegistration|ApplicationBuild|LocalDatabaseValidation|EndpointRegistration|ApplicationStartListen)|ExceptionType: [A-Za-z_][A-Za-z0-9_.`+]*)(?=\r?$)");
                        throw new InvalidOperationException("Local API exited before readiness. " +
                            string.Join("; ", diagnostic.Select(match => match.Value)));
                    }
                    try
                    {
                        using var response = await host.Client.PostAsJsonAsync("/api/interviews/schedule", new { });
                        if (response.StatusCode == HttpStatusCode.Unauthorized) return host;
                        throw new InvalidOperationException("Unexpected local API readiness response.");
                    }
                    catch (HttpRequestException) { await Task.Delay(100); }
                }
                throw new InvalidOperationException("Local API readiness timed out.");
            }
            catch { await host.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            if (!Process.HasExited) Process.Kill(entireProcessTree: true);
            await Process.WaitForExitAsync();
            await Task.WhenAll(Stdout, Stderr); // Deliberately never print child output.
            Process.Dispose();
        }
    }
}
