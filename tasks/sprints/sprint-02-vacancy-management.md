# Sprint 02 — Vacancy Management

**Goal:** Allow employers to create, review, edit, publish, and manage only their own vacancies.  
**Start:** 2026-08-15  
**End:** Open

## In Progress

- None

## Todo

- None

## Done

- [x] Implement vacancy list, filters, create, details, and edit actions
- [x] Scope every vacancy operation to the authenticated employer
- [x] Validate salary, closing date, and allowed workflow status
- [x] Preserve server-owned employer and posted-date values
- [x] Add accessible vacancy and accommodation fields to responsive Razor forms

## Session Log

### 2026-08-15 — Implement employer vacancy lifecycle
- What changed: Added the vacancy controller, list/create/edit/details views, shared vacancy form, and vacancy presentation styles.
- Why: Employers need to manage draft, published, and closed opportunities without accessing another employer's records.
- Status: Vacancy lifecycle and boundary validation compile successfully; database-backed runtime testing remains open for a seeded environment.
