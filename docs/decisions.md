# Decision Log

This file is append-only.

### 2026-08-15 — Preserve the established ASP.NET Core MVC structure
**Context:** The starter recommends feature ownership, while the shared team repository already organizes MVC code by controller, view, and model with role-specific subfolders.  
**Decision:** Keep the established MVC convention and treat each role subfolder as the feature boundary.  
**Alternatives:** Moving the application into a new `src/features` hierarchy was rejected because it would create broad merge conflicts with teammates.  
**Consequences:** Employer work remains isolated in Employer subfolders and integrates with the shared models and database context.

### 2026-08-15 — Enforce employer ownership in controller queries
**Context:** Numeric route IDs can be altered by a user and session login does not provide policy-based authorization.  
**Decision:** Every employer read or mutation must combine the requested record ID with the current employer ID.  
**Alternatives:** Trusting hidden form fields or adding a full identity migration was rejected for this module's current scope.  
**Consequences:** Cross-employer records return not found, and server-owned IDs are never accepted from forms.

