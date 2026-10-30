# ADR 0002: Modular monolith backend

- **Status:** Accepted by the project brief; project boundaries are proposed.
- **Date:** 2026-10-30

## Context

The MVP has related workflows with shared transactions and a single-school launch. It must remain maintainable as more schools and features are added, without taking on distributed operations prematurely.

## Decision

Build one deployable ASP.NET Core application organized as a modular monolith. Keep school management, membership/authorization, students/enrollment, attendance, daily reports, announcements, and audit as explicit feature modules.

Start with three production projects—API, Application, Infrastructure—and a test project. Do not split out a separate Domain project until independent compilation/reuse provides a concrete benefit.

## Alternatives considered

- One unstructured web project: fewer files initially, but weak dependency boundaries as features grow.
- Four production layers including a separate Domain project: viable later, but adds project ceremony before domain independence is established.
- Microservices, queues, and distributed transactions: rejected for MVP; no independent scaling/team/deployment requirement exists.
- Default use of CQRS/MediatR/Generic Repository: rejected absent a specific problem.

## Consequences and trade-offs

- One deployment and PostgreSQL transaction boundary simplify enrollment transfers and announcement publication.
- Feature ownership and dependency direction still require discipline and tests; project folders alone do not create modularity.
- API depends on Application; Infrastructure implements persistence/Identity integrations; Application remains independent of HTTP and EF Core.
- Extract a module/service only when scaling, team, isolation, or deployment evidence warrants the operational cost.

## References

- [ASP.NET Core architecture guidance](https://learn.microsoft.com/en-us/dotnet/architecture/)
- [EF Core architecture and testing](https://learn.microsoft.com/en-us/ef/core/testing/)
