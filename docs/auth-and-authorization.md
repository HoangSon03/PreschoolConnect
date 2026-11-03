# Authentication and Authorization

## Confirmed direction

Use ASP.NET Core Identity for user/password management and secure cookie authentication for the same-origin React + ASP.NET Core application. There is no public registration. Authentication, authorization, school membership, and student-account mapping are separate concepts.

## Identity model

- **Authentication:** Identity verifies credentials and creates the authenticated principal.
- **Authorization:** server policies/use cases decide whether that principal may perform an operation.
- **Tenant membership:** `SchoolMembership` ties a user to a school with active status and one or more school-scoped roles.
- **Student mapping:** a unique `StudentAccount` maps a dedicated parent-facing user to exactly one student. Resolve it from the authenticated user; do not authorize a supplied `StudentId`.
- **Platform administration:** not included in the MVP. If needed, introduce a separate platform-level role and operations boundary rather than treating a school admin as global.

## Role and access matrix (proposed baseline)

| Capability | SchoolAdmin | Teacher assigned to class | Parent mapped to student |
|---|---:|---:|---:|
| Manage school/class/year/membership | Yes, own school | No | No |
| Manage student profile/account/enrollment | Yes, own school | Read assigned students | Own mapped student, read-only |
| Record attendance | Yes, own school | Assigned class only | No |
| Draft/publish daily report | Yes, own school if approved | Assigned class only | No |
| Read daily report | All authorized school records | Assigned class | Published mapped-student records |
| Publish announcement | School-wide per permission | Assigned/authorized targets | No |
| Read announcement / update read state | School scope | School scope | Recipient rows for mapped student only |

Least privilege applies. Enforce school, class, student, account-status, and record-state conditions on the server, not only in routes or UI. Tests must include cross-school and wrong-student denials.

## Tenant context

Resolve the authenticated user's active membership server-side on each request. When a user has several memberships, any school selector must reference a verified active membership. Use the membership's school for scoped reads and writes. Parent queries derive school and student scope through `StudentAccount`; ignore client-provided school/student ownership for authorization.

Use explicit tenant-scoped application queries, membership/role checks, composite database keys, and authorization tests. EF Core global query filters are a convenience, never the only boundary. See [architecture](./architecture.md) and [database ERD](./database-erd.md).

## Audit actors and archived access

Business row auditing uses the authenticated Identity claim as its actor, never a
browser-supplied user ID. The persistence layer also checks active account/school
membership, while endpoint role checks remain mandatory. Deleted schools and
memberships cannot resolve tenant access. Pending migrations return an explicit
503 before querying the new audited business schema.

Archived business metadata and AuditLogs are available only through explicit
SchoolAdmin, resolved-school reads. Filter bypass does not grant cross-school or
platform administration. Non-request jobs require a named, scoped system
operation; null historical creators are not inferred from current membership.
See [audit and soft-delete policy](./audit-and-soft-delete.md).

## Cookie/session security

- HTTPS in production; `HttpOnly`, `Secure`, and an appropriate `SameSite` policy on auth cookies.
- Cookie authentication is ambient browser authority. Use ASP.NET Core antiforgery protection for state-changing requests; `SameSite` and HTTPS alone do not eliminate CSRF.
- Do not mutate state on GET. Use deliberate 401/403 API responses rather than browser-login redirects for API requests.
- Use framework sign-out and invalidate sessions after account deactivation, password reset, membership revocation, or security changes as supported by the selected Identity flow.
- Apply Identity lockout plus carefully tested rate limits to login and password/setup endpoints. Trust forwarded client IP headers only from configured proxies.
- Keep data-protection keys durable and protected across production instances; define key access/rotation and session revocation behavior before deployment.

## Account provisioning and first login

Administrators create accounts. The first-login/password-reset token must be short-lived, single-use, protected, and delivered through an identity-verified channel. The brief excludes email/SMS integrations, so **the initial handoff channel is unresolved and must be approved**. Do not display a reusable temporary password or expose reset tokens in logs. Deactivate accounts without deleting history or reassigning student identity implicitly.

## Official references

- [ASP.NET Core Identity (.NET 10 view)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)
- [Cookie authentication (.NET 10 view)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0)
- [ASP.NET Core antiforgery (.NET 10 view)](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
- [ASP.NET Core rate limiting (.NET 10 view)](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
