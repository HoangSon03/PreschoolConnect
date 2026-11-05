# Implementation Status

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

## Commit-snapshot verification

The isolated Teacher commit snapshot passed 95 backend tests with zero failures
or skips, including disposable loopback PostgreSQL, forward-only migration
guards and model/snapshot consistency. Its 19 frontend tests and TypeScript/Vite
production build passed. Student implementation is not part of this snapshot.
No ordinary application database or authenticated browser end-to-end test was used.

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

## Historical baseline before the JWT and Teacher updates

The following records describe the earlier baseline, not current
authentication, provisioning or Teacher functionality.

### Completed and verified

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

### Partial, not complete

- School membership/tenant groundwork and school setup exist, but role provisioning, school selection UI for users with multiple memberships, and broader authorization coverage remain incomplete.
- The client currently uses a single application page; it does not yet have feature routing or complete product workflows.
- No PostgreSQL-backed automated integration tests yet; the local migrations and readiness have been smoke-tested against PostgreSQL separately.
- Local PostgreSQL runtime folders were restored from the existing `.postgres\postgresql.zip` archive. The startup script does not yet extract missing runtime payloads on a clean checkout.
- Setup documentation, a dependable one-command startup experience, and CI remain incomplete.

### Not implemented

Teacher/class assignments, student accounts and enrollment/transfer history, attendance, daily reports, announcements, parent portal, audit workflows, accessibility review, and pilot operations.

### Environment notes

- .NET SDK 10.0.403 is available under the user-local `.dotnet` installation; system-wide SDK is 9.0.102. `scripts/verify.ps1` configures the local SDK.
- Node.js 24.22.2 and npm 11.19.1 are available.
- PostgreSQL 17.11 is local-only, bound to loopback, and intended for synthetic development data.
- Docker CLI and Graphify are unavailable. `dotnet-ef` 10.0.14 was used from a temporary location; it was not added to project dependencies.
- No cloud database, paid service, or real child data has been configured.

### Latest verification

- The equivalent checks from `scripts/verify.ps1` → passed: 13 backend tests passed and the production frontend build passed.
- PostgreSQL `pg_isready` → accepting connections on `127.0.0.1:5432`.
- API `/api/health` and `/api/health/ready` returned HTTP 200; readiness reported the database connected.
- Authenticated browser smoke test → successful sign-in over local HTTP, school workspace loading, school year creation, and class creation with synthetic values.

### Next implementation steps

1. Make local startup repeatable and document the separate PostgreSQL, API, and client processes.
2. Add school selection for users with multiple memberships and database-backed integration coverage.
3. Implement teacher assignments, enrollment/student lifecycle, attendance, reports, announcements, and parent access as separately tested vertical slices.
4. Establish CI and complete privacy, security, accessibility, backup/restore, and pilot-readiness reviews before real-world use.
