# Project Index

Canonical map of the important files in the repository. Update it whenever a file is added, moved, renamed, or changes scope.

## Workflow and Documentation

| Concern | File |
|---|---|
| Repository rules | `CLAUDE.md` |
| Cross-assistant rules | `AGENTS.md` |
| Current state | `.context/current.md` |
| Active sprints | `tasks/active.md` |
| Lessons | `tasks/lessons.md` |
| Architecture | `docs/architecture.md` |
| Decisions | `docs/decisions.md` |
| Documentation index | `docs/INDEX.md` |
| Environment template | `.env.example` |
| CI pipeline | `.github/workflows/ci.yml` |
| Sprint-log enforcement | `scripts/sprint-log/` |

## Application

| Concern | Location |
|---|---|
| Solution and web project | `DDAC.slnx`, `DDAC/DDAC.csproj` |
| Startup and dependency configuration | `DDAC/Program.cs` |
| Entity Framework context | `DDAC/Data/ApplicationDbContext.cs` |
| Shared database entities | `DDAC/Models/` |
| Login and registration | `DDAC/Controllers/SharedController/UserController.cs`, `DDAC/Views/Shared/` |
| Job Seeker feature | `DDAC/Controllers/JobSeeker/`, `DDAC/Views/JobSeeker/`, `DDAC/wwwroot/css/JobSeeker/` |
| Employer dashboard and guidance | `DDAC/Controllers/Employer/EmployerController.cs`, `DDAC/Views/Employer/Index.cshtml`, `DDAC/Views/Employer/HiringGuidance.cshtml` |
| Employer profile | `DDAC/Controllers/Employer/EmployerProfileController.cs`, `DDAC/Views/Employer/Profile.cshtml`, `DDAC/Views/Employer/EditProfile.cshtml` |
| Employer vacancies | `DDAC/Controllers/Employer/JobVacancyController.cs`, employer vacancy views, shared `_VacancyForm.cshtml` partial |
| Employer applications/interviews | `DDAC/Controllers/Employer/EmployerApplicationController.cs`, employer application and interview views |
| Employer inquiries | `DDAC/Controllers/Employer/EmployerInquiryController.cs`, employer inquiry views |
| Employer navigation/styles | `DDAC/Views/Employer/_EmployerSidebar.cshtml`, `DDAC/wwwroot/css/Employer/employer.css` |
| Migrations | `DDAC/Migrations/` |
