# Product Scope

## Status and decision labels

This is the planning baseline for Preschool Connect. Product goals and explicit rules below come from the project brief. Architecture proposals and unresolved business choices are marked **Recommended** or **Approval required**; they are not silently treated as settled.

## Product

Preschool Connect is a web communication product for preschool schools. It connects school administrators, teachers, and parents/guardians through school-managed accounts. The product is intended to become a commercial SaaS, but the MVP serves one school while preserving school ownership boundaries for future multi-school operation.

## Users and MVP capabilities

| User | MVP capabilities |
|---|---|
| School administrator | Manage school and school years; classes; student profiles and accounts; class teachers; enrollments and transfers; school announcements; attendance statistics; memberships and account status. |
| Teacher | View assigned classes and students; record class attendance; save and publish daily reports; create, copy, target, and publish announcements; view read statistics. |
| Parent/guardian | Sign in to the account mapped to exactly one student; view that student's profile, attendance, published reports, announcements, read status, and historical class context. |

### Confirmed product rules

- No public self-registration. Administrators provision accounts.
- At most one dedicated parent-facing account per student; one account maps to exactly one student.
- The authenticated account-to-student mapping is resolved server-side. A browser-supplied `StudentId` is never an authorization boundary.
- Parent contact details live on the student profile; no separate `Parents` table in the MVP.
- Student class changes create new enrollment history; do not overwrite prior enrollment records.
- Attendance and daily reports refer to the relevant enrollment, preserving historical class context.
- Announcements snapshot recipients at publication; later enrollment changes do not rewrite that snapshot.
- No chat, SMS/email/push delivery, tuition/payment, multiple guardian accounts, AI health/behavior assessment, or subscription billing in the MVP.

## Technology direction

The requested direction is .NET 10 LTS / ASP.NET Core Web API / EF Core / PostgreSQL / ASP.NET Core Identity with cookie authentication, plus React / TypeScript / Vite / React Router / TanStack Query / React Hook Form / Zod. PostgreSQL is required from local development through production. These are architectural targets, not packages or application code already present in this folder.

## Boundaries

- **Implement in the MVP:** core school workflows, server-side authorization, tenant isolation, auditability, tests, and fake-data development.
- **Prepare for later:** multiple school tenants in a shared PostgreSQL schema, safe school ownership keys, and modular business boundaries.
- **Defer:** billing, communications integrations, multi-guardian access, media storage, complex platform administration, microservices, and MCP integrations without an approved use case.
- Do not describe the product as production-ready until privacy, security, operations, backup/restore, and deployment requirements have been tested and reviewed.

## Approval-required product questions

1. What school time-zone identifier and calendar determine school-local attendance/report dates?
2. Are users permitted to hold multiple school roles at once, and can one human be both staff and a parent?
3. Any future retroactive enrollment correction workflow needs separate approval;
   the approved Student delivery uses immutable ended history and today-only
   transfers/withdrawals/re-enrollment, with gaps allowed. See [business rules](./business-rules.md#enrollment-history).
4. Should a class-wide attendance submission be atomic (all-or-nothing) or allow per-student results?
5. Who may edit a published report/attendance record, and what correction/audit trail is required?
6. Which students qualify at announcement publication time, including withdrawn students and effective-date boundaries?
7. How is a first-login setup credential delivered and identity verified without public registration or an email/SMS integration?
8. What retention, correction, export, and deletion rules apply to child and parent data?

See [business rules](./business-rules.md), [architecture](./architecture.md), and [implementation roadmap](./implementation-roadmap.md).
