# Preschool Connect agent guidance

## Scope and source of truth

This file applies only inside `PreschoolConnect/`. The parent repository contains a separate Personal Finance Tracker with its own root instructions and contracts; do not apply its npm-workspace, Express/SQLite, finance API, or architecture assumptions to this project. Do not modify files outside this subtree unless the user explicitly asks. Never inspect, print, commit, or upload the parent project's local SQLite data.

Product and engineering sources of truth:

1. [Product scope](docs/product-scope.md)
2. [Architecture](docs/architecture.md) and [database ERD](docs/database-erd.md)
3. [Business rules](docs/business-rules.md), [auth and authorization](docs/auth-and-authorization.md), and [security](docs/security.md)
4. [Roadmap](docs/implementation-roadmap.md), [reuse analysis](docs/reuse-analysis.md), and [third-party inventory](docs/third-party-components.md)
5. [Architecture Decision Records](docs/decisions/)

Do not silently turn a recommendation or approval-required item into a business rule.

## Engineering rules

- Keep child and parent data private. Use synthetic data only in prompts, logs, tests, and examples.
- Do not expose credentials, tokens, production data, or sensitive database contents to agents or external tools.
- PostgreSQL is the target from local development through production. Do not introduce SQLite, MongoDB, or another provider without explicit approval.
- Preserve school boundaries in every API, application use case, database relation, and test. Resolve tenant/student identity server-side; never trust browser-supplied ownership IDs.
- Use ASP.NET Core Identity and framework password hashing. Never implement custom password hashing or public self-registration.
- Research framework features, maintained packages, exact licenses, and dependency advisories before adopting code/packages. Record only actually adopted dependencies in the inventory.
- Prefer small, testable changes and existing framework capabilities. Avoid generic repositories, CQRS/MediatR, microservices, brokers, and custom abstractions without a demonstrated need.
- Read only the documentation relevant to the task. Before substantial work, inspect Git status and existing changes, then plan, implement, test, review the diff, and update directly related docs. Use small tasks and stable commits as checkpoints; never commit automatically.
- Do not install/upgrade system software or commit/deploy automatically without approval. Use `scripts/verify.ps1` for the existing backend tests and frontend build; extend it only with checks that are available and safe to run.
- Do not configure hooks that deploy, delete data, or perform uncontrolled edits.
- MCP is deferred: ordinary GitHub tools suffice unless issue/PR workflows become repetitive; official documentation can be read directly; PostgreSQL schema exploration can use local tools with fake data and read-only credentials if it becomes valuable. Each proposed server needs a concrete benefit, permissions review, context/privacy assessment, and approval. Never grant agents unrestricted production-data access.

## Tooling discovery caveat

This project is a sibling folder outside the PersonalFinanceTracker Git repository and currently has no Git metadata of its own. Check the actual working directory and source-control boundary before relying on Git status or diffs. The project instructions and `.github/copilot-instructions.md` are authoritative for PreschoolConnect.
