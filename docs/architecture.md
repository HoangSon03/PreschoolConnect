# Architecture

## Decision status

The product brief confirms a modular monolith, ASP.NET Core, EF Core, PostgreSQL, Identity, and a React SPA. The approved authentication update replaces cookies with short-lived JWT Bearer tokens. Tenant controls and business modules remain incomplete; see [implementation status](./implementation-status.md).

## Runtime shape

```mermaid
flowchart LR
  Browser["React + TypeScript SPA"] -->|"same-origin HTTPS, JWT Bearer + CSRF token"| API["ASP.NET Core Web API"]
  API --> App["Application use cases and authorization"]
  App --> Infra["EF Core persistence and integrations"]
  Infra --> DB[("PostgreSQL")]
  API --> Identity["ASP.NET Core Identity"]
  Identity --> DB
```

**Recommended deployment shape:** serve the SPA and API under one origin (for example, reverse-proxy `/api` to ASP.NET Core). A separate API origin is not selected; if later required, revisit CORS, credentialed antiforgery requests, and CSRF controls together.

## Project boundaries

The current solution has three production projects and a test project:

```text
server/
  PreschoolConnect.Api/             HTTP, middleware, authentication, composition
  PreschoolConnect.Application/     use cases, domain rules, authorization policies
  PreschoolConnect.Infrastructure/  EF Core, PostgreSQL, Identity persistence
  PreschoolConnect.Tests/           unit and focused integration tests
client/
  src/app/                           routing and application composition
  src/features/{students,classes,attendance,daily-reports,announcements,parent-portal}/
  src/components/                    shared UI components
  src/lib/                           API client and cross-feature utilities
```

This avoids a separate Domain project before its value is clear while keeping HTTP, use cases, and persistence dependencies distinct. Organize by feature inside these boundaries. Split a domain project only when independent domain reuse or compile-time isolation justifies it.

Dependency direction: API → Application; Infrastructure implements application-facing persistence/identity abstractions and is composed by API; Application does not depend on ASP.NET Core HTTP or EF Core. Frontend API calls and server-state behavior stay behind the client feature/lib boundary.

The current API still registers most Minimal API handlers, request validation,
and EF queries inline in `Program.cs`; Application primarily contains models
and shared authorization/audit context. Extract a feature endpoint group or
application operation when extending that feature, rather than treating the
target layering above as already complete.

Do not introduce Generic Repository, Unit of Work, CQRS, MediatR, a DI container, microservices, queues, or a migration framework beyond EF Core migrations by default. Add a pattern only for a concrete problem.

## Modular monolith

Keep school management, enrollment, attendance, daily reports, announcements, membership/authorization, and audit as feature modules in one deployable application and database. Modules may share a transaction where a business operation requires it, but should access another module through explicit application contracts rather than arbitrary table writes.

## Tenant boundary

- A school owns every school-scoped business row. Include `SchoolId` on these rows.
- Resolve an active school membership server-side from the authenticated user. If a user belongs to several schools, an explicit selection is accepted only after the server verifies that membership.
- Parent scope is derived from the authenticated account's `StudentAccount` → `Student` → `School` relationship. Never authorize from a request-body `SchoolId` or `StudentId`.
- Use tenant-scoped application queries and authorization on every read/write. Add composite unique keys and composite foreign keys to prevent cross-school relationships.
- EF Core global query filters may reduce accidental omissions, but are not the sole security boundary: they can be bypassed and do not replace authorization or explicit scoped operations.
- PostgreSQL row-level security is a possible later defense-in-depth layer, not an MVP substitute. Table owners and roles with bypass privileges can bypass policies; pooled-connection context and migration roles require careful design.

## Authentication and authorization

Use ASP.NET Core Identity for user/password lifecycle, framework hashing, lockout, and token primitives. API issues validated 15-minute JWT Bearer tokens; React holds them only in memory, without refresh tokens. Infrastructure implements Application authentication contracts using Identity and live memberships. Account status and stamp are checked on each Bearer request; logout revokes all account tokens. Authorization separately evaluates school membership/role, class assignment, and student mapping. Roles never replace the tenant boundary. See [authentication contracts](./auth-and-authorization.md).

## Standard auditing and soft deletion

The six existing business entities share `BaseAuditableEntity`. Central
ApplicationDbContext SaveChanges/SaveChangesAsync processing assigns UTC metadata
from the authenticated actor or an explicit scoped system operation, preserves
creation metadata, converts deletes to D updates, rejects deleted-record updates,
and appends minimal AuditLogs transactionally. Identity and composite permission
joins retain separate lifecycles; permission changes still emit historical events.
The named SoftDelete query filter does not replace tenant/role authorization.
PostgreSQL guards, restrictive FKs, partial uniqueness and authorized archived
queries complement EF behavior. Future historical facts/recipient snapshots must
not inherit parent-active query behavior automatically.

See [audit and soft-delete policy](./audit-and-soft-delete.md) and
[migration notes](./audit-migration-notes.md) for scope, deletion rules, bypass
limitations, safe legacy backfill, and explicit no-restoration/forward-only policy.

## Current implementation, target, and deferred work

| Implemented foundation | Targeted but incomplete | Deferred |
|---|---|---|
| Identity + JWT Bearer and CSRF; active membership-based tenant resolution; school-scoped year/class APIs; PostgreSQL persistence; auditing and soft delete on the six existing business entities. | School selection UI for multiple memberships; broader authorization coverage; student/enrollment/attendance/report/announcement workflows; fuller separation of API handlers and application operations. | Tenant provisioning/billing, database-per-tenant, RLS as primary enforcement, distributed services, external notifications, private-media system, unrestricted agent database access. |

Database migrations are not run during ordinary API startup. Use the explicit
operator-controlled migration command in the [migration notes](./audit-migration-notes.md).

## Workspace boundary

PreschoolConnect is located in a sibling folder to `PersonalFinanceTracker` and is not part of that finance application's Git worktree. Keep their source, dependencies, instructions, and local data separate. Finance-specific SQLite/API assumptions do not apply to PreschoolConnect.

## References

- [ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)
- [EF Core global query filters](https://learn.microsoft.com/en-us/ef/core/querying/filters)
- [PostgreSQL row security](https://www.postgresql.org/docs/current/ddl-rowsecurity.html)
- [Npgsql EF Core provider](https://www.npgsql.org/efcore/)
- [Npgsql date/time handling](https://www.npgsql.org/doc/types/datetime.html)
