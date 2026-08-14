# Repository Rules

## Session Start

1. Read `INDEX.md`.
2. Read `.context/current.md`.
3. Read `tasks/active.md` and every sprint file it lists.
4. Read the Active section in `tasks/lessons.md`.

## Workflow

- Every task follows plan → implement → verify → update the relevant sprint file.
- For non-trivial work, write and communicate the plan before editing application code.
- After a user correction, append a lesson under `tasks/lessons.md` → Active.
- Never close or archive a sprint unless the user explicitly says to close it.
- The lowest-numbered sprint in `tasks/active.md` is current unless work clearly belongs to a later sprint.

## Sprint Session Log

After every implementation session, append an entry under the relevant sprint's `## Session Log`:

```markdown
### YYYY-MM-DD — Summary
- What changed: files or components modified
- Why: reason for the work
- Status: working state and anything open
```

The scripts in `scripts/sprint-log/` and hooks in `.claude/settings.json` enforce this for compatible assistants.

## Folder Ownership

| Concern | Location |
|---|---|
| Master file map | `INDEX.md` |
| Current project state | `.context/current.md` |
| Active sprint list | `tasks/active.md` |
| Sprint files | `tasks/sprints/` |
| Archived sprints | `tasks/archive/` |
| Lessons | `tasks/lessons.md` |
| Architecture and decisions | `docs/` |
| Application source | `DDAC/` |
| Utility scripts | `scripts/` |

## Documentation Rules

- Check `INDEX.md` before searching for a known concern.
- Update `INDEX.md` whenever a file is added, moved, renamed, or changes scope.
- Update `docs/INDEX.md` whenever documentation is added or changes scope.
- `docs/decisions.md` is append-only.

## Code Rules

- Follow the existing ASP.NET Core MVC separation of controllers, models, views, and static assets.
- Keep employer data scoped to the authenticated employer ID on every read and mutation.
- Validate user input at controller boundaries and use anti-forgery validation for mutations.
- Preserve server-owned values such as owner IDs, posted dates, and workflow statuses instead of trusting form data.
- Avoid hardcoded workflow values by centralizing reusable constants.
- Handle missing and unauthorized records explicitly.

## Browser and System Testing Protocol

Browser and end-to-end test sessions are find-and-report only. Test each checklist item once. On failure, record the finding and stop that test item; do not edit application code or rerun it during the testing session. Fixes require a separate implementation task.

## Before Done

- [ ] Relevant behavior was verified.
- [ ] Build and available tests pass, or findings are documented.
- [ ] No duplicate feature files were introduced.
- [ ] Relevant sprint session logs were updated.
- [ ] `.context/current.md` reflects sprint or stack changes.
- [ ] `INDEX.md` reflects file additions and scope changes.
- [ ] The response ends with the mandatory Read Aloud section.

