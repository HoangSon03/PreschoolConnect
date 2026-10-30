# Security and Privacy Baseline

Preschool Connect handles child and parent information. This document sets requirements for design and future verification; it is not a claim that the system is secure or production-ready.

## Threat boundaries

- Every school is a tenant boundary. A user must not read or mutate another school's data.
- Parents may access only the student mapped to their authenticated account and only published parent-visible records.
- Teachers are limited to assigned classes and permitted actions; administrators are limited to their school.
- The UI is untrusted. Validate and authorize every API operation server-side.
- Development, tests, sample content, screenshots, and prompts use synthetic data only. No production student data or secrets are available to agents.

## Required controls

1. **Authentication:** ASP.NET Core Identity, framework password hashing, no public registration, account activation/deactivation, lockout, recovery, logout, and secure session lifecycle.
2. **Browser session:** HTTPS in production; `HttpOnly`, `Secure`, appropriate `SameSite`; antiforgery protection for state changes; no state-changing GET; CORS limited to the intended same-origin arrangement. Development permits HTTP-only loopback by matching cookie `Secure` policy to the request; production always requires secure cookies.
3. **Authorization:** policy/use-case checks for account state, school membership, role, class assignment, and student mapping. Never trust a client `SchoolId` or `StudentId`.
4. **Tenant persistence:** school columns, explicit tenant-scoped queries, composite same-school foreign keys, uniqueness constraints, and cross-tenant API/data tests. Query filters/RLS may be defense in depth only.
5. **Input and output:** validate all request bodies, identifiers, dates, status transitions, batch limits, and target sets; avoid returning private fields not required by the caller.
6. **Abuse controls:** rate-limit credential/setup endpoints, lock out repeated failed logins, cap request/body sizes, and review operational rate limits under load. Rate limiting is not DDoS protection.
7. **Audit/logging:** audit high-impact changes with minimal metadata; do not log passwords, tokens, report/health text, or unnecessary student/contact data. Redact sensitive values and restrict log access/retention.
8. **Data lifecycle:** define retention, correction, export, deletion, backup, restore, and breach-response processes before real student data is used. Encrypt transport; protect backups and secrets at rest through deployment-managed controls.
9. **Dependencies/supply chain:** use locked, supported versions; inspect direct/transitive dependency licenses and vulnerability advisories before adoption and continuously after lockfile creation. Do not adopt unlicensed source.
10. **AI/tooling:** no unrestricted production DB/MCP access. Use least privilege, fake data, reviewed patches, and no secret-bearing logs or prompts.

The initial API slice now provides `/api/auth/csrf`; login and logout require the returned token in `X-CSRF-TOKEN`. Login is limited to 10 requests per minute per remote IP, in addition to Identity lockout. These controls are covered by focused tests but have not had a production security review, proxy-aware rate-limit configuration, or load assessment. The local PostgreSQL cluster uses development trust authentication and binds only to loopback; never expose or reuse it for production data.

## Sensitive fields and minimization

Student name, date of birth, parent contact details, attendance, and especially `HealthNote` require role-limited access and a documented purpose. Collect only what a school workflow requires. Avoid putting private content in analytics, exception messages, test fixtures, audit metadata, or AI context.

## Multi-tenant testing

Before any pilot, test at minimum:

- user with no membership, inactive membership, or wrong role is denied;
- teacher cannot access an unassigned class, including by changing a route/body ID;
- parent cannot read another student's profile/report/attendance/announcement, including a same-school student;
- a user cannot cross school boundaries through joins, lists, update/delete routes, bulk operations, or announcement targets;
- composite constraints reject cross-school student/class/enrollment/recipient links;
- tenant scope stays correct for concurrent requests and reused database contexts;
- admin and migration credentials do not silently bypass intended controls.

Use relational integration tests with PostgreSQL for constraints and query behavior; unit tests alone cannot prove database isolation.

## Compliance and production caveat

Before handling real Vietnamese child data, obtain qualified review of current Vietnamese personal-data, child-data, consent/guardian, retention, data-subject, cross-border-transfer, and incident-notification obligations. No legal conclusion is made here. Establish a data-protection impact/risk review with each pilot school before deployment.

## References

- [ASP.NET Core antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
- [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [EF Core query filters](https://learn.microsoft.com/en-us/ef/core/querying/filters)
- [PostgreSQL row security and owner/bypass behavior](https://www.postgresql.org/docs/current/ddl-rowsecurity.html)
