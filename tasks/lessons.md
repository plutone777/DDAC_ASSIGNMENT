# Lessons

Append-only record of user corrections and durable process improvements.

## Active

### 2026-08-15 — Match centralized views to controller routing
**Problem:** Employer views were centralized under `Views/Employer`, but actions on controllers named `EmployerProfile`, `JobVacancy`, `EmployerApplication`, and `EmployerInquiry` relied on the default MVC view-location convention.
**Rule:** When a controller's name differs from its feature view folder, return the explicit feature view path from every GET action and every validation-error POST branch.
**Why:** A successful compile does not validate runtime view discovery; explicit paths preserve the requested folder structure and prevent view-not-found errors.

### 2026-08-15 — Explicitly route centralized feature partials
**Problem:** Explicitly locating a full view under `Views/Employer` does not change how Razor resolves partial names; `_EmployerSidebar` and `_VacancyForm` were still searched for in controller-named folders.
**Rule:** When shared feature partials live outside the default controller view folder, reference them with rooted paths such as `~/Views/Employer/_EmployerSidebar.cshtml` from every consuming view.
**Why:** Main-view routing and partial-view routing are separate discovery operations, so both must be explicit when using a centralized feature folder.

## Internalized

<!-- Move a lesson here only after it has not been violated for at least two sprints. -->
