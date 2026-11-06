# ADR 0004: Shared PostgreSQL schema with explicit school isolation

- **Status:** Implemented and formally approved by the user on 2026-11-03.
- **Date:** 2026-10-30

## Context

The MVP launches with one school but is intended to become a multi-school SaaS. School-owned data includes students, classes, school years, enrollments, attendance, reports, and announcements. The brief prefers a shared PostgreSQL database/schema and requires server-side tenant resolution.

## Decision

The current code uses one PostgreSQL schema with `SchoolId` on school-owned
entities, resolves active school access through verified `SchoolMembership`
records, and models the parent mapping through `StudentAccount`. It uses
school-scoped queries and composite relationships where implemented. The user
formally approved this architectural decision on 2026-11-03. Product workflows
and complete cross-tenant tests remain incomplete.

EF Core global query filters may add a safe default but are not the only control. PostgreSQL RLS may be evaluated later as defense in depth after connection-pooling, migration ownership, and privileged-role behavior are designed.

## Alternatives considered

- Separate database/schema per school: stronger physical separation but adds provisioning, migrations, operations, and cross-school reporting complexity not justified for the MVP.
- Tenant ID accepted from a request body/header/host without membership validation: rejected; the caller can spoof another school.
- Global query filters alone: rejected; filters can be bypassed and do not establish authorization or valid cross-table school relationships.
- Finbuckle.MultiTenant: reviewed but not selected for the MVP. Explicit context is small enough initially; the repository currently announces a commercial maintenance fee whose terms were unavailable during this review.
- RLS as the only enforcement: rejected; PostgreSQL documents owner and BYPASSRLS behavior, and application authorization is still required.

## Approval record

The user confirmed formal approval for this ADR during the remediation on
2026-11-03. The school-year scope separately confirms active school members may
view their own school's year/class lists, while mutations remain
SchoolAdmin-only.

## Consequences and trade-offs

- One shared schema is operationally simple and supports school-scoped indexes and common migrations.
- A missed `SchoolId` predicate could expose data unless authorization, database constraints, and tests are layered.
- Composite foreign keys prevent many cross-school links even when application validation fails.
- Any admin/maintenance endpoint that intentionally crosses schools needs a separate explicit authorization path and audit.
- Shared infrastructure means a tenant query defect has a larger blast radius; isolation tests and monitoring are release gates.

## References

- [EF Core global query filters and multi-tenancy](https://learn.microsoft.com/en-us/ef/core/querying/filters)
- [PostgreSQL row security](https://www.postgresql.org/docs/current/ddl-rowsecurity.html)
- [Finbuckle.MultiTenant source and licensing notice](https://github.com/Finbuckle/Finbuckle.MultiTenant)
