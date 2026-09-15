# Local interview scheduling host

Independent process, build-time reference to the existing DDAC assembly. It reuses the
contracts, SQL Server DbContext, entities and scheduling service without starting MVC.
This is a local service-boundary proof, not an independently deployable cloud package:
the web assembly and its transitive dependencies remain a packaging dependency.
Scheduling follows Employer MVC ->
InterviewApiClient -> real HTTP -> separate LocalApi -> InterviewSchedulingService.

Run the built local API project under the Windows account owning MSSQLLocalDB.
Supply `DDAC_INTERVIEWS_CALLER_KEY` through the process environment (at least 32 random
characters). Never commit, print or send this value to browser JavaScript. Optional
`DDAC_INTERVIEWS_PORT` defaults to 5244. Bind address is always loopback.
The application refuses startup without a key or a verified local database.

POST `/api/interviews/schedule`, with JSON matching ScheduleInterviewRequest.
Headers: `X-Caller-Key` and `X-Employer-ID`. The latter is a server assertion,
accepted only after key authentication; the account must be an active Employer.
Possession of the key allows that trusted caller to assert Employer identities.
This is NOT end-user authentication; the MVC client derives identity from
its authenticated server context. No CORS or browser cookie authentication is enabled.
Loopback HTTP is for this local proof only, not suitable for remote deployment.

200: scheduling succeeded; 400: validation_failed; 401: unauthorized;
404: not_found (identical for ownership denial and absence); 500: internal_error.
Errors are safe structured responses, never raw exceptions. No request/SQL logging.
Basic DTO validation is explicit; all scheduling business rules stay in the service.

Send InterviewDate as local wall-clock ISO text WITHOUT a timezone suffix/offset,
matching the existing datetime-local form. No explicit UTC conversion is performed.
Final timezone policy must be reviewed before any multi-machine/cloud deployment.

Database target is fixed to `(localdb)\MSSQLLocalDB`, `DDAC_Employer_LocalTest`, with
Windows authentication. Startup checks DB_NAME and IsLocalDB before listening.
No alternate connection override, automatic migration, database creation or cloud
service registration exists. Only fictional local records are appropriate here.

Host tests launch the built host as a separate dotnet process on a temporary
loopback port. Secrets are generated in memory and passed only in the child's
environment and HTTP headers. Tests stop the child in finally/disposal and retain
synthetic fixtures. No MVC server process is needed. Run each regression suite
separately and stop at the first failure. MVC integration tests verify scheduling through LocalApi.
