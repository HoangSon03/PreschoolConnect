# Authentication and Authorization

## Confirmed direction

Use ASP.NET Core Identity for user/password management and JWT Bearer authentication for the same-origin React + ASP.NET Core application. This replaces the original cookie transport by explicit product decision. There is no public registration. Authentication, authorization, school membership, and student-account mapping are separate concepts.

## Identity model

- **Authentication:** Identity verifies credentials and creates the authenticated principal.
- **User model:** existing `ApplicationUser : IdentityUser<Guid>` supplies Id, Email, PasswordHash, security stamp and lockout fields; `CreatedUtc` supplies creation time. No duplicate user table, plaintext password, single global Role column, or schema change is needed. Identity's registered `PasswordHasher<ApplicationUser>` handles hashing and verification.
- **Role names:** API roles are Admin, Teacher and Parent. Admin is an alias of the existing SchoolAdmin membership grant, not a platform administrator. Live active school memberships supply roles; global Identity role rows are not sufficient for access. School-specific handlers still verify the selected membership's roles.
- **Authorization:** server policies/use cases decide whether that principal may perform an operation.
- **Tenant membership:** `SchoolMembership` ties a user to a school with active status and one or more school-scoped roles.
- **Student mapping:** a unique `StudentAccount` maps a dedicated parent-facing user to exactly one student. Resolve it from the authenticated user; do not authorize a supplied `StudentId`.
- **Platform administration:** not included in the MVP. If needed, introduce a separate platform-level role and operations boundary rather than treating a school admin as global.

## Role and access matrix (proposed baseline)

| Capability | SchoolAdmin | Teacher assigned to class | Parent mapped to student |
|---|---:|---:|---:|
| Manage school/class/year/membership | Yes, own school | No | No |
| List school years and classes | Yes, own school | Yes, own school membership | No school-wide list; future views must be scoped to the mapped student |
| Manage student profile/account/enrollment | Yes, own school | Read assigned students | Own mapped student, read-only |
| Record attendance | Yes, own school | Assigned class only | No |
| Draft/publish daily report | Yes, own school if approved | Assigned class only | No |
| Read daily report | All authorized school records | Assigned class | Published mapped-student records |
| Publish announcement | School-wide per permission | Assigned/authorized targets | No |
| Read announcement / update read state | School scope | School scope | Recipient rows for mapped student only |

Least privilege applies. Enforce school, class, student, account-status, and record-state conditions on the server, not only in routes or UI. Tests must include cross-school and wrong-student denials.

The approved school-year scope allows any active school member to view the
school's year list; only SchoolAdmin may mutate years or create
classes. Parent/student accounts are mapped separately and do not receive a
school-wide list. The current GET endpoints implement this membership-based
read rule; teacher assignment filtering is required for future student data,
not the year directory. The approved Teacher implementation now narrows Teacher
class directory results and year class counts to currently assigned classes.
SchoolAdmin retains school-wide directory access.

## Teacher personnel and assignment APIs

All `/api/teachers` routes require JWT and an active server-resolved membership.
`X-School-Id` may select only a verified membership. Admin alone creates/updates
profiles, lists eligible account links and changes assignments. Teacher may
list/read only their linked profile; other-teacher/cross-school IDs return 404,
unauthorized roles return 403, missing authentication returns 401.

| API | Contract |
|---|---|
| `GET /api/teachers` | Authorized profile DTOs; React performs search, status/class filtering and pagination over this authorized result set. |
| `GET /api/teachers/{id}` | Same-scope profile plus authorized assignment details. |
| `POST /api/teachers` | Create the UI's personnel profile; 201 with DTO/Location. |
| `PUT /api/teachers/{id}` | Update personnel fields/status and first explicit account link; 200. |
| `GET /api/teachers/accounts` | Admin-only active same-school Teacher account choices. |
| `POST /api/teachers/{id}/assignments` | `{ classroomId, role }`; starts now, validates same-school live teacher/class/year; 200 with updated DTO. |
| `DELETE /api/teachers/{id}/assignments/{assignmentId}` | End now, never physically remove; 200 with updated DTO. |

Profile writes accept `{ name, birthday, gender, phone, email, address, degree,
graduated, university, experience, certificate, startDate, contract, work,
isActive, userId }`. Dates use ISO `yyyy-MM-dd`; linked `userId` is a validated
target, never the caller's authority. Ownership/audit/unknown JSON fields are
rejected. DTOs contain `{ id, code, profile, assignments, lastActionAtUtc }`;
assignment DTOs contain IDs/names for class/year, role and server UTC start/end.
No credentials are returned. Mutations retain CSRF protection; invalid input is
400, missing scoped rows 404, duplicates/concurrency/link conflicts 409.
Account/profile/assignment history is never inferred from browser ownership IDs.

`ApplicationUser.SchoolId` and `ApplicationUser.StudentId` remain in the current
Identity model and initial migration but have no runtime references beyond
their declarations. Current tenant and parent mapping use
`SchoolMembership`/`StudentAccount` instead. Do not use the legacy fields for
new authorization. Any removal requires an authorized inventory of existing
database values and a reviewed forward migration; no column is dropped by this
change.

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

## JWT contracts and session security

- `GET /api/auth/csrf` returns `{ requestToken }`. Fetch a fresh token with the current Bearer header before login/logout or business mutations; retain the HttpOnly antiforgery cookie.
- `POST /api/auth/login` takes `{ email, password }` and `X-CSRF-TOKEN`. Success returns `{ accessToken, tokenType: "Bearer", expiresIn: 900, expiresAtUtc, user: { userId, email, fullName, isActive, roles } }`, with `Cache-Control: no-store`; no auth cookie is issued. Invalid input/CSRF is 400; absent, wrong, inactive or locked credentials are generic 401; rate limit is 429. Pending database migrations return 503 without applying them. Email is required and unique under Identity validation; password policy remains the existing framework policy.
- `GET /api/auth/me` requires `Authorization: Bearer <accessToken>` and returns the user DTO. Old login cookies do not authenticate APIs.
- `POST /api/auth/logout` requires Bearer and CSRF. Success rotates the Identity security stamp, invalidating **all** tokens for that account, not just this browser. Failed revocation is not reported as success.
- JWTs expire after 15 minutes, with no expiry grace or refresh token. Validate HS256, signing key, issuer, audience, not-before and expiry. Tokens contain only subject, token ID, public role names, validity metadata and an opaque keyed session marker; never email, passwords, raw stamps, child data or ownership IDs.
- On every Bearer request, check active account and current security stamp, then reload live membership roles. Password resets revoke tokens; deactivation denies requests immediately; membership removal denies affected school/role access immediately. Signing-key rotation invalidates all tokens.
- React stores access tokens only in memory. Reload, expiry or server 401 requires login again and clears protected data. No localStorage/sessionStorage token persistence. No refresh flow is added: simpler revocation and storage at the cost of a new login every 15 minutes.
- Retain existing antiforgery and same-origin controls; HTTPS is mandatory outside local Development. Protect signing keys and data-protection keys across instances. Trust forwarded client IP only from configured proxies. Identity lockout remains five failures/five minutes, alongside the 10/minute/IP login limiter.
- HTTP endpoints/token serialization live in API; application auth contracts live in Application; Identity password, stamp and membership operations live in Infrastructure. Tenant business handlers are unchanged.

## Account provisioning and first login

Administrators create accounts. The first-login/password-reset token must be short-lived, single-use, protected, and delivered through an identity-verified channel. The brief excludes email/SMS integrations, so **the initial handoff channel is unresolved and must be approved**. Do not display a reusable temporary password or expose reset tokens in logs. Deactivate accounts without deleting history or reassigning student identity implicitly.

For verified disposable local development only, `--provision-local-admin` reads
`Bootstrap__AdminEmail`, `Bootstrap__AdminPassword`, `Bootstrap__SchoolId` and
`Bootstrap__ConfirmDisposableTarget=true` from private operator configuration.
It requires Development and loopback PostgreSQL, creates a new Identity account
and SchoolAdmin membership for an existing active school in one transaction,
and refuses existing accounts (no reset, activation or elevation). It never
migrates, seeds a school, or starts HTTP. Ordinary startup does not provision
admins. Legacy `Seed__AdminPassword` is no longer used; optional synthetic
school/role initialization requires `Seed__Enabled=true`. Production account
handoff/provisioning remains an operator-reviewed deployment task.

## Official references

- [ASP.NET Core Identity (.NET 10 view)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)
- [JWT Bearer authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [ASP.NET Core antiforgery (.NET 10 view)](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
- [ASP.NET Core rate limiting (.NET 10 view)](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
