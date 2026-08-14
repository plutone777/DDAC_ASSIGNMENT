# Lessons

Append-only record of user corrections and durable process improvements.

## Active

### 2026-08-15 — Match centralized views to controller routing
**Problem:** Employer views were centralized under `Views/Employer`, but actions on controllers named `EmployerProfile`, `JobVacancy`, `EmployerApplication`, and `EmployerInquiry` relied on the default MVC view-location convention.
**Rule:** When a controller's name differs from its feature view folder, return the explicit feature view path from every GET action and every validation-error POST branch.
**Why:** A successful compile does not validate runtime view discovery; explicit paths preserve the requested folder structure and prevent view-not-found errors.

## Internalized

<!-- Move a lesson here only after it has not been violated for at least two sprints. -->
