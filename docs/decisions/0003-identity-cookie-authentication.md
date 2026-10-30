# ADR 0003: ASP.NET Core Identity and secure cookies

- **Status:** Accepted by the project brief; configuration details remain subject to security review.
- **Date:** 2026-10-30

## Context

The first application is a same-origin React SPA and ASP.NET Core API with school-provisioned accounts, local passwords, account activation, and no public registration. Browser cookies are requested for the initial authentication model.

## Decision

Use ASP.NET Core Identity for user/password management, framework hashing, account lockout and token primitives. Use secure cookie authentication for browser sessions. Build application authorization separately around active school membership/role, class assignment, account status, and parent-to-student mapping.

Use HTTPS, `HttpOnly`, `Secure`, an appropriate `SameSite` policy, antiforgery protection on state-changing requests, explicit logout/session lifecycle, and rate limiting on credential/setup endpoints. Never implement custom password hashing.

## Alternatives considered

- Custom auth/password storage: rejected as unnecessary security-sensitive reinvention.
- JWT bearer tokens in browser storage: not selected for the initial same-origin app; they introduce different storage/rotation concerns and do not remove the need for authorization.
- External identity provider: deferred; school-managed local accounts are required and no provider is selected.
- ASP.NET Core Identity global roles as the complete authorization model: rejected because global roles do not prove school/class/student scope.

## Consequences and trade-offs

- Framework account primitives reduce custom security code but still require careful configuration and patching.
- Cookie authentication is convenient for same-origin browsing but requires CSRF protection because browsers attach cookies automatically.
- Account provisioning and first-login token delivery remain an unresolved product decision because no email/SMS integration is in scope.
- API clients must handle unauthenticated/forbidden responses as API responses, not depend on interactive redirects.

## References

- [ASP.NET Core Identity, .NET 10](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)
- [Cookie authentication, .NET 10](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0)
- [Antiforgery, .NET 10](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
- [Rate limiting, .NET 10](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
