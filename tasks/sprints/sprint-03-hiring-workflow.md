# Sprint 03 — Hiring Workflow

**Goal:** Complete employer application review, interview scheduling, guidance, inquiries, UI integration, and verification.  
**Start:** 2026-08-15  
**End:** Open

## In Progress

- None

## Todo

- None

## Done

- [x] Review employer-owned applications and update allowed statuses
- [x] Schedule interviews and automatically shortlist eligible applications
- [x] Display published career resources and announcements
- [x] Create and track inquiries with active career advisors
- [x] Complete responsive employer navigation and styling
- [x] Configure .NET 10 CI and compile the solution successfully

## Session Log

### 2026-08-15 — Complete employer hiring and support workflows
- What changed: Added application review, status update, interview scheduling, hiring guidance, inquiry controllers and views, layout integration, and .NET CI.
- Why: This completes the employer feature scope across candidate review, inclusive hiring support, and advisor communication.
- Status: The .NET 10 solution builds with zero errors. Existing shared-model nullability warnings remain, and the NuGet vulnerability feed was unreachable in the restricted environment. No automated test project exists, so database-backed behavior was not executed.

### 2026-08-15 — Run live smoke-test preflight
- What changed: No application code changed; checked the .NET and LocalDB tooling and attempted to initialize the tracked Entity Framework schema.
- Why: A populated local database is required before launching and exercising the Employer workflow in a browser.
- Status: Failed once during database initialization because LocalDB could not create the automatic `MSSQLLocalDB` instance. Per the system-testing protocol, the test stopped without retries or fixes; server startup, HTTP routes, login, and Employer pages remain unverified in this pass.

### 2026-08-15 — Fix Employer button destinations and view discovery
- What changed: Added a shared Employer view-root constant and updated profile, vacancy, application, interview, and inquiry actions to return explicit files from `Views/Employer`; added the routing lesson to the project log and cleared the resolved LocalDB blocker.
- Why: Live testing showed that buttons reached the correct controllers, but MVC searched controller-named folders rather than the centralized Employer view folder required by the project structure.
- Status: All affected normal and validation-error view returns now target existing Employer files, and the project compiles with zero errors. A server restart and fresh live smoke test are required to load and exercise the new controller assembly.
