# Preschool Connect agent guidance

These instructions apply only to this standalone `PreschoolConnect/` repository. Do not modify or inspect the adjacent Personal Finance Tracker or its local data.
This file and the linked canonical project docs define project requirements; skills only describe task-specific workflows.

## Product and engineering sources of truth

Read only the documents relevant to the task:

1. [Product scope](docs/product-scope.md)
2. [Architecture](docs/architecture.md) and [database ERD](docs/database-erd.md)
3. [Business rules](docs/business-rules.md), [authorization](docs/auth-and-authorization.md), and [security](docs/security.md)
4. [Roadmap](docs/implementation-roadmap.md), [reuse analysis](docs/reuse-analysis.md), and [third-party inventory](docs/third-party-components.md)
5. [Architecture Decision Records](docs/decisions/)

Recommendations and approval-required items are not approved requirements.

## Non-negotiable safeguards

- Keep child and parent data private. Use synthetic data in prompts, tests, logs, and examples; never disclose credentials, tokens, production data, or sensitive database contents.
- Enforce school/student authorization server-side. Resolve tenant identity from verified membership; never trust browser-supplied ownership IDs. Preserve school boundaries in APIs, use cases, relations, and tests.
- PostgreSQL is the sole database target. Do not introduce SQLite, MongoDB, or another provider without explicit approval.
- Use ASP.NET Core Identity and framework password hashing. Never implement custom password hashing or public self-registration.
- Do not connect to production or inspect the ordinary application database. Manual database access, migrations, seeds, or writes require explicit approval and a verified disposable target. Automated integration tests must use the existing loopback-only disposable-database safeguards.
- Before adopting a dependency, verify maintained status, exact license, and security advisories; record only adopted dependencies in the inventory.
- Preserve existing uncommitted changes. Do not install/upgrade system software, commit, push, or deploy unless explicitly requested.

## Delivery workflow

1. Do not run a repository-wide audit before implementation. Audits are separate and use `project-audit` only when explicitly requested.
2. Inspect only task-relevant instructions, files, dependencies, and existing changes. Expand scope only when implementation or verification requires it; preserve unrelated user work.
3. Use `feature-delivery` for feature implementations and issue fixes, including its final-report format. Also use `database-safety` whenever persistence, queries, migrations, tenant isolation, auditing, seeds, or database configuration are affected.
4. Follow existing patterns; keep changes small and avoid unnecessary abstractions, duplicated logic, dependencies, and documentation. Update the existing canonical docs when behavior or configuration changes.
5. Implement the requested functionality and run proportionate, relevant checks. Do not commit, push, or deploy unless requested.

## Skill and tool routing

- This file is the sole authoritative project-instruction source; task-specific procedures belong in skills.
- Use Graphify for repository-relationship questions only when its CLI is available and the checked-in graph matches current source. Verify claims against source. If Graphify is unavailable or stale, inspect targeted source directly. Do not install Graphify or refresh its graph unless asked; any refresh must retain the documented code-only privacy exclusions.
- Use Archify only when a diagram or explainer is requested. It is not a default reporting or audit tool.
- Add a `.github/hooks/` hook only for a deterministic check supported by the active Copilot harness, with understood failure behavior and a safe, repository-local implementation. Do not assume hooks transfer between harnesses. If no suitable check can be tested, add no hook.
- MCP is deferred. Add a server only after documenting concrete benefit, permissions, privacy impact, and approval; never grant agents unrestricted production-data access.

## Repository boundary

This project is a separate Git repository adjacent to `PersonalFinanceTracker`. Verify the actual repository root, branch, status, and source-control boundary before relying on Git status or diffs.
