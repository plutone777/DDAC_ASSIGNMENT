# Local Employer baseline protection

## Integration verification

The MVC fixture starts the built LocalApi in a separate dotnet process before
creating the MVC test host. It supplies the real typed HTTP client with in-memory
runtime options, a generated caller key and the loopback child URL.
Sync/async fixture disposal stops the child.
The direct host suite opts out of the fixture's child and uses its own launcher.

Build the full solution, then run each filter separately, stopping on a genuine failure:
`EmployerBaselineTests`, `InterviewSchedulingServiceTests`, `InterviewContractTests`,
`InterviewHostTests`, `InterviewMvcIntegrationTests`.

The MVC integration test uses real TCP HTTP to the child for successful scheduling
and validation. A pass-through observer checks identity headers and the five-field JSON
without printing secrets. Controlled 401/500/malformed/unexpected/timeout responses are
injected only for fault checks; the client/controller are real, not replaced by the
in-process scheduling service. Unavailability is checked by stopping the real child.
The five-second timeout is injected BEFORE forwarding and the actual local snapshot is
checked unchanged. This does not resolve an uncertain timeout AFTER a remote commit.
No retry/idempotency behavior is added. One integration run creates one interview.

The baseline, scheduling service, transport contract, independent host and MVC-to-LocalApi
integration suites verify the local implementation.

Run from the solution directory under the Windows user who owns MSSQLLocalDB:

```powershell
dotnet build DDAC.slnx --configuration Release
dotnet test tests/DDAC.Employer.Tests/DDAC.Employer.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~EmployerBaselineTests" --logger "console;verbosity=detailed"
if ($LASTEXITCODE -eq 0) {
    dotnet test tests/DDAC.Employer.Tests/DDAC.Employer.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~InterviewSchedulingServiceTests" --logger "console;verbosity=detailed"
}
```

The ordered baseline xUnit test stops at its first failure and prints 37 named checkpoints.
It covers the login-warning sequence, scheduling from both Submitted and Under Review,
cross-owner reads/writes, and missing/invalid anti-forgery tokens on all six Employer POST actions.

One ordered focused test (24 checkpoints) covers the in-process
scheduling service: invalid date/type/location, MVC binding failure, direct service ownership,
unchanged data after rejection, HTTP validation feedback, normalization/optional fields,
and preservation of Shortlisted/Rejected/Hired statuses. Run the two tests separately as
above to stop the whole verification sequence at its first failure. The tests exercise
the separate LocalApi host locally; this is not a cloud deployment.

The in-process ASP.NET host uses real routes, sessions, Razor rendering, authentication,
anti-forgery middleware and the SQL Server provider. No production helper is needed.
It replaces only test-host database registration and forbids S3 service resolution.
No external application server or AWS credentials are used.

The only database target is `(localdb)\MSSQLLocalDB`, database `DDAC_Employer_LocalTest`.
The database must already exist with migration `20260814024546_InitialCreate` and the
current model snapshot. The runner verifies the database identity and test-host target
before seeding. It never creates databases, runs migrations, or alters schema.

Every run inserts fresh, uniquely labelled LOCAL TEST AUTO fixtures: four fictional
users, three profiles, one vacancy, two applications and one inquiry. Successful
scheduling adds two interviews in the baseline test. The focused test creates its own
fixtures, changes only its own synthetic application's status, and adds three interviews.
Passwords are generated in memory and never printed.
Resume references are placeholders, not S3 objects.

Fixtures are retained for inspection on success or failure. Existing manual fixtures
are not reused or changed. Repetition uses new IDs, so prior runs do not affect assertions;
records accumulate until manual cleanup. No automatic delete/reset occurs.

This is a fail-fast HTTP/database regression suite, not visual, cloud, load, or exhaustive
validation testing. Login redirect-warning handling is separate from interview-scheduling behavior.
