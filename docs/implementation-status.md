# Implementation Status

Status verified on 2026-10-31. PreschoolConnect is a standalone project,
separate from `PersonalFinanceTracker`, with its own Git repository on `main`.
This update has not been committed.

## Completed and verified

- .NET 10 solution with API, Application, Infrastructure, and test projects.
- React + TypeScript + Vite client.
- ASP.NET Core Identity cookie login, current-user endpoint, and development-only administrator seeding when `Seed__AdminPassword` is configured.
- Antiforgery validation for login/logout and school-management POST requests; login rate limit of 10 requests/minute per remote IP.
- Liveness endpoint (`/api/health`) and PostgreSQL-backed readiness endpoint (`/api/health/ready`).
- PostgreSQL 17.11 runs from the project-local portable distribution at `127.0.0.1:5432`; migrations for Identity, schools, memberships, roles, school years, and classes have been applied to the local development database.
- Server-validated school membership context. A client-selected school is accepted only when the authenticated user has an active membership; ambiguous multi-school accounts must select a school, and unauthorized school access is denied.
- School year and class APIs with SchoolAdmin authorization, validation, and school/year relationship constraints.
- Client sign-in, API/database status display, and school-year/class create/list screens for a selected school membership.
- The test/build commands from `scripts/verify.ps1` passed in this environment: backend tests **13/13 passed** and Vite production build passed.
- Local API and Vite smoke checks passed; authenticated browser testing created and displayed a synthetic school year and class.

## Partial, not complete

- School membership/tenant groundwork and school setup exist, but role provisioning, school selection UI for users with multiple memberships, and broader authorization coverage remain incomplete.
- The client currently uses a single application page; it does not yet have feature routing or complete product workflows.
- No PostgreSQL-backed automated integration tests yet; the local migrations and readiness have been smoke-tested against PostgreSQL separately.
- Local PostgreSQL runtime folders were restored from the existing `.postgres\postgresql.zip` archive. The startup script does not yet extract missing runtime payloads on a clean checkout.
- Setup documentation, a dependable one-command startup experience, and CI remain incomplete.

## Not implemented

Teacher/class assignments, student accounts and enrollment/transfer history, attendance, daily reports, announcements, parent portal, audit workflows, accessibility review, and pilot operations.

## Environment notes

- .NET SDK 10.0.403 is available under the user-local `.dotnet` installation; system-wide SDK is 9.0.102. `scripts/verify.ps1` configures the local SDK.
- Node.js 24.22.2 and npm 11.19.1 are available.
- PostgreSQL 17.11 is local-only, bound to loopback, and intended for synthetic development data.
- Docker CLI and Graphify are unavailable. `dotnet-ef` 10.0.14 was used from a temporary location; it was not added to project dependencies.
- No cloud database, paid service, or real child data has been configured.

## Latest verification

- The equivalent checks from `scripts/verify.ps1` → passed: 13 backend tests passed and the production frontend build passed.
- PostgreSQL `pg_isready` → accepting connections on `127.0.0.1:5432`.
- API `/api/health` and `/api/health/ready` returned HTTP 200; readiness reported the database connected.
- Authenticated browser smoke test → successful sign-in over local HTTP, school workspace loading, school year creation, and class creation with synthetic values.

## Next implementation steps

1. Make local startup repeatable and document the separate PostgreSQL, API, and client processes.
2. Add school selection for users with multiple memberships and database-backed integration coverage.
3. Implement teacher assignments, enrollment/student lifecycle, attendance, reports, announcements, and parent access as separately tested vertical slices.
4. Establish CI and complete privacy, security, accessibility, backup/restore, and pilot-readiness reviews before real-world use.
