# Implementation Roadmap

Implementation proceeds milestone-by-milestone from the verified state in [implementation status](./implementation-status.md). A successful build alone is not production readiness.

## Milestone 00 environment snapshot

Inspected on 2026-10-30 without installing or upgrading system software:

- The project pins .NET SDK 10.0.403 in `global.json`. That SDK exists under the user-local `.dotnet` installation; `scripts/verify.ps1` configures `DOTNET_ROOT` and `PATH` so it can build. The system-wide SDK is 9.0.102.
- .NET 10.0.11 runtime, Node.js 24.20.0, npm 11.19.0, and Git 2.41.0 are available.
- PostgreSQL 17.7 is now running from the existing project-local archive; its runtime `share` and `lib` payloads were restored, and the initial schema migration was applied.
- Docker CLI and Graphify are unavailable. EF CLI 10.0.14 was used from a temporary location; it was not installed into the project or system.
- PreschoolConnect is a sibling directory with its own Git repository; files remain uncommitted.
- `scripts/verify.ps1` has passed with the latest backend, schema, and frontend changes; see [implementation status](./implementation-status.md).

| Milestone | Outcome | Gate |
|---|---|---|
| 00 — Architecture foundation | Product/architecture/security docs, ERD, reuse analysis, third-party inventory, instructions, and ADRs exist. | Complete as a documentation foundation; keep docs aligned with implementation. |
| 01 — Repository/toolchain foundation | Separate Git repository, .NET 10 solution, React/Vite client, verification script, PostgreSQL 17.7, and initial migrations exist. Basic startup instructions and CI remain. | Substantially complete locally; make startup repeatable and add CI before pilot use. |
| 02 — Identity and school boundary | Identity login/logout, antiforgery validation, login rate limiting, school memberships, server-resolved tenant context, and negative tenant tests exist. School selection UI and complete authorization coverage remain. | Incomplete until school-scoped authorization and isolation coverage are comprehensive. |
| 03 — School setup and student lifecycle | School years/classes APIs, constraints, tests, and an initial management UI exist. Teacher assignments, student profiles/accounts, enrollment transfer/history, and audit behavior remain. | Same-school constraints and transfer/history integration tests pass. |
| 04 — Attendance and daily reports | Class attendance workflow, draft/publish reports, parent read-only views, audit/correction handling. | Batch semantics, date/time rules, publishing rules, privacy/access tests approved and pass. |
| 05 — Announcements | School/class/student targeting, publication-time recipient snapshot, parent read state, copying to drafts, read statistics. | Snapshot boundary and concurrent publication semantics approved and tested. |
| 06 — Pilot readiness | Accessibility/usability, security and dependency review, backup/restore, observability, retention, incident procedures, operational runbooks, and legal/privacy review with a school. | Documented pilot acceptance and operational ownership; no unresolved critical/high risks. |

## Current next task

Next, make local startup repeatable and improve setup documentation; then continue Milestone 02 school selection and authorization coverage. Do not configure a public/paid database or use real child data without explicit approval.

## AI-assisted development workflow

- For substantial changes: inspect the relevant code/docs and Git status, plan a small change, implement it, run focused checks, review the diff, update the source-of-truth docs, and commit only when requested.
- Keep each task bounded and load only the docs needed for that task. Treat AI output as a proposal to inspect, not as verified implementation.
- At Milestone 01, add a `scripts/verify.ps1` that runs formatting/static checks, tests, and builds. Hooks may invoke only verified checks; never deploy, delete data, or apply uncontrolled edits.
- No MCP server is necessary for Milestone 00. Reconsider GitHub MCP only if issue/PR tasks become repetitive; use official docs directly; consider database tooling only with synthetic local data and least-privilege read-only access. No production student data or broad DB permissions for agents.

## Deliberately deferred

Payments/subscriptions, chat and external notification delivery, multiple guardians, photos/videos, AI assessment, platform billing/provisioning, microservices, MCP servers, production-data agent access, and any feature not approved in the MVP.
