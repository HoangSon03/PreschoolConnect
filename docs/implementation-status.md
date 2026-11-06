# Implementation Status

PreschoolConnect is a standalone repository, separate from
`PersonalFinanceTracker`.

## Classroom counts, detail and metadata editing

- Real class cards show effective student counts, active assigned teachers,
  room and optional capacity. Selected-year teacher totals use distinct IDs.
- Added `/admin/classes/:id` with the supplied image/Figma detail layout:
  searchable 15-row roster, full-roster print, teacher sidebar, information and
  unsupported-document tabs, Monday–Friday routine and current-week note.
  Admin-only `/admin/classes/:id/edit` edits name, room, informational capacity,
  bounded non-overlapping activities and Monday-keyed weekly notes. Existing
  name/year creation and Teacher assignment workflows remain unchanged.
- Added strict GET/PUT detail contracts, request-time JWT/membership/assignment
  checks, optimistic timestamp conflict detection and minimal audit field names.
  Parent cannot access class list/detail; teachers cannot modify classes.
- `20261105091912_ClassroomDetails` preserves existing rows and adds JSON/metadata
  constraints and a forward-only migration. It was generated and tested against
  disposable loopback PostgreSQL. After separate explicit approval and confirmation
  of the local test-data target, the operator command applied it to local development
  following a private backup/archive check. No seed or account changes ran;
  readiness passed and a nonexistent synthetic login returned 401 rather than
  the pending-schema 503. No authenticated local-browser login was claimed.
- Browser smoke checks used synthetic API intercepts for pagination (15 of 16),
  search, complete print roster (16), unsupported documents, metadata save payload
  and mobile overflow, empty/error/retry states and Teacher read-only controls.
  **115 related backend tests and 25 frontend tests passed, zero failures/skips**;
  Release compilation and TypeScript/Vite production build passed.
  The independent Classroom publication snapshot subsequently passed the full
  **132 backend tests**, 25 frontend tests and production build with no skips.
  Browser checks are not authenticated browser end-to-end checks
  against the ordinary application database. Local icons come from the supplied
  Figma detail context; the list frame fetch hit the Figma tool limit, so existing
  list components were preserved instead of claiming a newly inspected frame.

## Student profiles and enrollment history

- Implemented the existing Student list, record-ID/name/contact search,
  class/status filters, new-this-month tab, pagination, real counts, print,
  create/edit, detail and profile navigation without fixture data or redesign.
- Extended the existing Student model with guardian/contact and optional
  health/care fields. The displayed student code is exactly the database record
  UUID. Profiles do not provision accounts or infer account links from email.
- Added audited StudentInClass enrollment periods with same-school composite
  student/class FKs and the explicitly approved bundled btree_gist exclusion
  constraint. Initial admission may be past; subsequent admission, transfer
  and withdrawal take effect today in Asia/Ho_Chi_Minh. Periods naturally expire
  at the class year's end; transfers close the previous row and create the next
  transactionally. Ended history is immutable and retained parents cannot be deleted.
- JWT/current membership/CSRF are enforced server-side. Admin manages;
  Teacher reads only current assigned-class students and eligible current
  enrollments, while Parent reads only their server-mapped student/history.
  Parent UI initialization no longer requests staff-only class directories.
- Attendance, tuition and document storage are explicitly outside this delivery;
  their panels show unsupported states, not sample records or upload actions.
- Focused Student/Teacher/JWT/Classroom/audit/tenant verification passed
  **99 backend tests, zero failures/skips**, including actual isolated PostgreSQL
  queries, concurrent enrollments/transfers, audit rollback and raw SQL guards.
  **22 frontend tests** and TypeScript/Vite production build passed.
  The independent Student publication snapshot subsequently passed the full
  **117 backend tests**, 22 frontend tests and production build with no skips.
  Browser smoke checks used synthetic intercepted API fixtures for list/search,
  create/save payload, edit prefill, unsupported tabs and failure/retry;
  this is not authenticated browser end-to-end verification against the local DB.
- After separate explicit approval and confirmation of the disposable local
  target, migration `20261104102931_StudentProfilesAndEnrollment` was applied
  using the operator command after creating/checking a private backup archive.
  No seed, account creation or reset was run. The local API was resumed;
  API/readiness, Vite proxy and CSRF checks returned 200.

## Teacher profiles and assignments

- Implemented the confirmed personnel list/search/class-status filter,
  pagination, real counts, print, create/edit and profile navigation using the
  existing screen layout and shared design components.
- Added school-scoped Teacher profiles and immediate many-to-many class
  assignments with retained end history. Optional account linking validates
  active same-school Teacher membership; profiles never create credentials or
  infer Identity from email.
- Personnel APIs enforce JWT, current membership and CSRF. Admin manages;
  Teacher reads only their own linked profile and currently assigned classes;
  Parent is denied. The shared class directory and year class counts now
  respect Teacher assignment scope.
- New forward migration `20261104095259_TeacherProfiles` includes composite FKs,
  uniqueness and audit/history guards. After explicit approval and confirmation
  of the disposable local target, it was applied using the operator command
  after creating and checking a private backup archive; no seed was run.
- Timetables and binary document storage were explicitly excluded from this
  delivery; real pages show clear unsupported states, not mock data.
- Focused Teacher/JWT/Classroom/audit/tenant verification passed 77 backend
  tests with zero failures/skips on a separate disposable PostgreSQL cluster.
  The 19 frontend tests and TypeScript/Vite production build also passed.

## JWT authentication update

- Replaced Identity login cookies with 15-minute JWT Bearer access tokens.
  Existing Identity user/password storage, framework hashing and lockout are
  reused; no schema or business-module change. Admin aliases SchoolAdmin;
  active membership remains the authority for each school's access.
- Login, current-user and logout contracts are documented in
  [authentication and authorization](./auth-and-authorization.md). Logout and
  password resets revoke account tokens; every Bearer request checks active
  account, stamp and current membership roles. Pending schema is an explicit 503.
- React stores tokens only in memory and clears protected data on expiry/401.
  Reload requires login. No refresh flow or UI redesign.
- Startup requires private JWT configuration and defaults to no seed writes.
  Explicit disposable-local admin provisioning uses environment configuration;
  no public registration, hardcoded password, implicit creation or elevation.
- Focused verification: **65 backend tests passed, zero failed/skipped**,
  including actual PostgreSQL Identity/provisioning/revocation and pending-schema
  handling on a freshly initialized isolated loopback cluster. Existing
  SchoolYear/Class authorization and tenant tests passed unchanged apart from
  Bearer login transport. **16 frontend tests**, TypeScript and Vite build passed.
  The test cluster was stopped and removed; the ordinary database was not
  migrated or seeded. Production account handoff and deployment key management
  remain operator responsibilities.

## Classroom backend - 2026-11-04

- Confirmed scope: keep the existing real name/year create, list/search/year
  filter and basic-information dialog. Existing GET/POST `/api/classes` and
  React wiring are reused; richer preview fields and edit/delete workflows
  are out of scope.
- Class names now ignore case within a year (including Vietnamese letters),
  with trimming, 100-character validation and conflict responses for concurrent
  inserts. The original year association is immutable in EF and PostgreSQL.
- Added `20261104080000_ClassroomIntegrity`; no application database was inspected
  or migrated. An operator must apply it explicitly after checking legacy names.
  See [migration notes](./audit-migration-notes.md) and the
  [UI/API contract](./design/figma-screen-mapping.md#classroom-api-contract).
- Focused Classroom/SchoolYear/audit checks passed **35 backend tests, no skips**,
  using a freshly initialized isolated loopback PostgreSQL 17.11 cluster that
  was shut down and removed afterwards. The existing **8 frontend tests** and
  TypeScript/Vite production build also passed. No dependencies were added.
  The ordinary database was not accessed; nothing was committed or deployed.

## Previous status - 2026-11-03

- The API no longer applies EF migrations during ordinary startup. Operators
  run `dotnet run --no-launch-profile --project
  .\server\PreschoolConnect.Api\PreschoolConnect.Api.csproj -- --migrate`
  explicitly. Startup requires `ConnectionStrings:DefaultConnection`; the
  migration command may use `ConnectionStrings:MigrationConnection` or fall
  back to the default connection for local development.
- Vite development/preview and the Docker Compose PostgreSQL port bind to
  loopback by default. A pull-request/main-branch verification workflow now
  includes .NET tests, PostgreSQL integration tests on an isolated service,
  frontend tests and a production build; the workflow itself has not run here.
- Startup database operations were moved out of `Program.cs`; migrations only
  run with the explicit `--migrate` command, while development data initialization
  remains development-only and logs failures.
- Docker Compose and CI both pin PostgreSQL 17.11. Dependency audits on
  2026-11-03 reported no vulnerable NuGet packages and zero npm vulnerabilities.
- The baseline checks passed with 27 backend tests; after these changes,
  `scripts/verify.ps1` passed with **28 backend tests and 7 optional PostgreSQL
  tests skipped**, **8 frontend tests**, and a TypeScript/Vite production build
  (326.31 kB JavaScript, 97.63 kB gzip). The optional PostgreSQL connection was
  unset, and the ordinary database was not contacted. The verification script
  uses the user-local SDK when present and otherwise resolves `dotnet` from PATH;
  `global.json` selects the SDK version.
- Graphify CLI was unavailable on PATH. The existing report is dated
  2026-11-01 and predates the 2026-11-03 audit migration; no graph refresh or
  query was performed.
- Figma MCP's current OAuth/tool state was not independently verified. Existing
  Figma documents record conflicting observations; see the integration note.
- The repo currently has pre-existing local frontend/documentation changes.
  They were preserved and not staged or committed.

## Frontend update - 2026-11-02

The original single-screen client has been reorganized into a shared shell,
feature pages and API helpers. All supplied screen exports now have synthetic
previews: dashboard, students/teachers/classes with profiles and forms, attendance,
events, announcements, fees, reports, settings and permissions. Search, filters,
pagination, form validation and local interactions work without persistence.
The real class list/search/year filter, class/year creation, session restoration
and logout remain connected to existing APIs. No new backend, role or fee policy
was introduced. Type-checked frontend build, 4 frontend test groups and all 13
backend tests pass; desktop/tablet/mobile browser smoke checks passed.

Production workflows remain incomplete; Help and original visual assets are
still missing. The mapping enumerates 22 screens plus one UI reference, not 23
application screens. See
[redesign status](./design/figma-implementation-status.md) for source references,
API gaps, browser verification and known visual differences.
The dated baseline below describes the earlier state where not superseded.

## School-year management - 2026-11-03

- New shared manager at `/preview/settings/years` and `/admin/settings/years`;
  the real settings entry opens it as well. List/search/date-phase filter,
  add/edit dialogs, locked dates for used years and delete confirmation.
- Preview CRUD changes only its local sample list and resets on page reload.
  It does not mutate class fixtures elsewhere or use APIs/localStorage.
- Added real PUT/DELETE endpoints, school-scoped class counts, admin/CSRF checks,
  duplicate-name and inclusive-overlap validation. Used-year date edits and
  deletion are forbidden; no class cascade or default-year policy.
- PostgreSQL 17 Unicode name index and per-school serialized trigger guards
  added through a migration. Legacy violations stop migration without changes;
  pending migrations block real class/year writes with an explicit 503.
- Verified: **22 backend tests passed**, including 3 optional PostgreSQL
  migration/integrity/concurrency tests on a separate disposable cluster.
  **8 frontend test groups passed**, strict TypeScript/Vite build passed.
  Browser sample CRUD, conflicts/cancel/reopen, real API-stub CRUD/header checks,
  Teacher read-only controls and 320/390/768/1440 px page/dialog overflow checks
  passed. Browser API stubs are synthetic; this is not a live authenticated
  persistence check against the ordinary development database.
- The disposable cluster was separate from ordinary local data; no production
  or existing school database was inspected or automatically migrated. No new
  dependencies, system installs, commits or deployments.

## Standard auditing and soft delete - 2026-11-03

- Implemented a shared audit base and centralized sync/async EF persistence for
  School, SchoolYear, Classroom, Student, SchoolMembership and StudentAccount.
  Authenticated actor assignment, explicit system scopes, immutable creation
  metadata, D-update rejection and soft-delete query filters are covered.
- Added separate append-only AuditLogs for business changes and permission
  grant/revoke events. Logs contain references and allowlisted field names, never
  business values. Administrative archived/audit reads require own-school
  SchoolAdmin authorization; no restoration or purge endpoint exists.
- Added a new generated migration/designer/snapshot, safe unknown-actor backfill,
  restrictive actor/membership FKs, active-only uniqueness and database guards.
  Existing migrations were not changed. Pending schema initialization blocks
  tenant resolution before audited columns are queried.
- Verified: **34 backend tests passed, zero skipped**, including **7 PostgreSQL
  integration tests** across legacy and audit migration/backfill, CHECK/FK/index
  behavior, stale/concurrent writers, preserved references and failed-write retry.
  **8 frontend test groups passed**, strict TypeScript/Vite build passed, and
  `git diff --check` passed. No frontend implementation or deployment behavior
  was changed for auditing.
- PostgreSQL tests used a separate disposable loopback cluster and synthetic
  databases. The ordinary database on port 5432 was unavailable and was not
  started or migrated. Applied migrations there could not be confirmed.
- No new dependencies, system installations, commits, pushes or deployments.

## Historical baseline - superseded

The following baseline was recorded before the later dated frontend,
school-year, auditing, and remediation updates above. Do not treat its older
test counts, implementation gaps, environment claims, or next steps as current.
The audit/soft-delete update described in
[the audit policy](./audit-and-soft-delete.md) and
[migration notes](./audit-migration-notes.md) supersedes the earlier physical
year-delete behavior. Row auditing and minimal historical AuditLogs are implemented
for the six existing business entities; future product/audit workflows below
remain unimplemented. The ordinary PostgreSQL database was unavailable during this
update and was not automatically migrated.

- .NET 10 solution with API, Application, Infrastructure, and test projects.
- React + TypeScript + Vite client.
- Historical Identity cookie login/development administrator seed is superseded by the JWT update above.
- Antiforgery validation for login/logout and school-management POST requests; login rate limit of 10 requests/minute per remote IP.
- Liveness endpoint (`/api/health`) and PostgreSQL-backed readiness endpoint (`/api/health/ready`).
- The earlier environment snapshot reported PostgreSQL 17.11 at `127.0.0.1:5432`; the current runtime and ordinary database migration state have not been independently verified.
- Server-validated school membership context. A client-selected school is accepted only when the authenticated user has an active membership; ambiguous multi-school accounts must select a school, and unauthorized school access is denied.
- School year and class APIs with SchoolAdmin authorization, validation, and school/year relationship constraints.
- Client sign-in, API/database status display, and school-year/class create/list screens for a selected school membership.
- The test/build commands from `scripts/verify.ps1` passed in this environment: backend tests **13/13 passed** and Vite production build passed.
- Local API and Vite smoke checks passed; authenticated browser testing created and displayed a synthetic school year and class.

## Historical partial snapshot - superseded where noted

- School membership/tenant groundwork and school setup exist, but role provisioning, school selection UI for users with multiple memberships, and broader authorization coverage remain incomplete.
- At this snapshot, the client had a single application page and lacked feature routing and complete product workflows; see the later frontend update below.
- Historical as of 2026-10-31: no PostgreSQL-backed automated integration tests yet. This was superseded by the 2026-11-03 update above, which records seven PostgreSQL integration tests.
- Local PostgreSQL runtime folders were restored from the existing `.postgres\postgresql.zip` archive. The startup script does not yet extract missing runtime payloads on a clean checkout.
- Setup documentation, a dependable one-command startup experience, and CI were incomplete at this snapshot. The later remediation added explicit migration instructions, a verification workflow, and SDK-path fallback to `scripts/verify.ps1`; the workflow has not yet run on GitHub.

## Not implemented

Teacher/class assignments, student enrollment/transfer history, attendance,
daily reports, announcements, parent portal, accessibility review, and pilot
operations remain unimplemented. Audit workflows were added in the later
2026-11-03 update above.

## Environment notes

- `global.json` pins .NET SDK 10.0.403. `scripts/verify.ps1` prefers the user-local `.dotnet` installation when present and otherwise resolves `dotnet` from PATH.
- Node.js 24.22.2 and npm 11.19.1 are available.
- PostgreSQL 17.11 is local-only, bound to loopback, and intended for synthetic development data.
- Docker CLI and Graphify are unavailable. `dotnet-ef` 10.0.14 was used from a temporary location; it was not added to project dependencies.
- No cloud database, paid service, or real child data has been configured.

## Historical verification - superseded

- The equivalent checks from `scripts/verify.ps1` → passed: 13 backend tests passed and the production frontend build passed.
- PostgreSQL `pg_isready` → accepting connections on `127.0.0.1:5432`.
- API `/api/health` and `/api/health/ready` returned HTTP 200; readiness reported the database connected.
- Authenticated browser smoke test → successful sign-in over local HTTP, school workspace loading, school year creation, and class creation with synthetic values.

## Historical next steps - superseded

The current next-step recommendation is in the
[implementation roadmap](./implementation-roadmap.md).
