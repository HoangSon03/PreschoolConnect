# ADR 0006: Reuse-first engineering and dependency review

- **Status:** Accepted by the project brief; package choices remain proposals.
- **Date:** 2026-10-30

## Context

The product should build on maintained framework capabilities and packages rather than duplicate solved infrastructure. It may become a commercial SaaS, so licenses, maintenance, security, and transitive dependencies matter.

## Decision

Before meaningful implementation, evaluate in this order: framework feature, maintained package, small adapted implementation, independent implementation. For each adopted package or copied source, record exact name/version/source/license, use, modifications, notices, maintenance/security findings, and transitive dependencies in [the inventory](../third-party-components.md). Never copy source without an identifiable license. Do not install dependencies during Milestone 00.

Use built-in ASP.NET Core Identity and logging as the default. Recommended React candidates (TanStack Query, React Hook Form, Zod) and test/UI tools remain unapproved until a pinned stable set is checked against licenses, advisories, compatibility, and the product's needs.

## Alternatives considered

- Implement every capability from scratch: rejected for authentication, validation, caching, and testing infrastructure.
- Add packages by popularity/stars without review: rejected; popularity is not proof of fit, security, or license compatibility.
- Copy public repository code without checking its license: prohibited.
- Install a broad stack before scope approval: rejected; it creates supply-chain and maintenance obligations before need is proven.

## Consequences and trade-offs

- Research takes effort, but reduces unnecessary custom code and catches commercial-use constraints early.
- Every external package adds a transitive dependency, update, and vulnerability-management obligation.
- No version is “adopted” until it is in an approved manifest/lockfile and passes security/license review.
- Security status remains version-specific; this milestone did not perform a full dependency advisory scan because no dependency graph exists.

## References

- [ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)
- [Reuse analysis](../reuse-analysis.md)
- [Third-party inventory](../third-party-components.md)
