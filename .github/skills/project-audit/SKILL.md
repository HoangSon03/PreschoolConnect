---
name: project-audit
description: "Perform a read-only audit, review, or evidence-based project summary for PreschoolConnect only when the user explicitly requests an audit. Do not audit before or as a prerequisite to implementation."
---

# Project audit

Follow the project rules in `AGENTS.md`; this workflow is read-only.

1. Confirm the repository boundary and inspect Git status/diff. Preserve all existing changes and do not stage or edit files.
2. Read only the relevant project docs and inspect source/tests for evidence. Use the existing Graphify snapshot only when current; do not install tools, rebuild Graphify, or use Archify unless explicitly requested.
3. Do not connect to or inspect any database, run migrations/seeds, consume external design quotas, or access production data. Prefer already available test results; run only safe, non-mutating checks when needed.
4. Distinguish implemented behavior, preview/mock behavior, proposals, and unknowns. Include file and line evidence where practical, severity/priority, impact, recommendation, confidence, and verification limits.
5. Report test/security results with dates and scope. Separate current checks from historical claims and state clearly when CI, PostgreSQL, Figma, or deployment behavior was not verified.
6. Make no fixes as part of the audit. Offer remediation separately; implement only after the user requests it.
