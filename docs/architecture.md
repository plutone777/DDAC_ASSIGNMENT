# Architecture

## Stack

| Layer | Technology | Purpose |
|---|---|---|
| UI | Razor Views and Bootstrap 5 | Server-rendered responsive pages |
| Backend | ASP.NET Core MVC on .NET 10 | Controllers, sessions, validation, and routing |
| Data | Entity Framework Core 10 and SQL Server | Relational persistence and migrations |
| Authentication | Existing session-based login | Role and user identity stored in server session |
| CI | GitHub Actions | Restore and compile on pushes and pull requests |

## Folder Structure

The repository follows the existing ASP.NET Core MVC convention. Role-specific controllers, views, and CSS are grouped in subfolders while shared entities remain in `Models/`.

```text
DDAC/
├── Controllers/{Employer,JobSeeker,...}/
├── Data/
├── Models/
├── Views/{Employer,JobSeeker,Shared,...}/
└── wwwroot/css/{Employer,JobSeeker,...}/
```

## Key Patterns

- Controllers obtain the current user ID and role from session and scope all employer queries by that ID.
- POST actions validate anti-forgery tokens, boundary input, and record ownership.
- Server-owned properties are assigned in controllers rather than trusted from submitted forms.
- Shared workflow strings are centralized in the employer controller foundation.
- Views use Razor tag helpers and a shared employer sidebar partial.

## External Services

No external service is required by the employer module. The teammate Job Seeker branch includes optional Amazon S3 resume storage that is not yet present on `main`.

