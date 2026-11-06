---
name: database-safety
description: "Safely handle PostgreSQL, EF Core persistence, queries, migrations, tenant isolation, auditing, seeds, or database tests in PreschoolConnect. Use whenever a task affects these areas; pair with feature-delivery for feature work."
---

# Database safety

Follow the project rules in `AGENTS.md`; they are authoritative.

1. Inspect only task-relevant instructions, files, dependencies, and database/authorization docs; expand scope only when necessary.
2. PostgreSQL is the only approved provider. Never add SQLite, MongoDB, or another provider without explicit approval.
3. Do not connect to production or inspect the ordinary application database. Do not run manual queries, migrations, seeds, or writes without explicit approval and confirmation of a disposable target.
4. Automated integration tests must use the existing loopback-only, disposable-database safeguards and synthetic data. Never bypass their host/database checks or run them against an ordinary database.
5. Preserve school boundaries in queries, relationships, and tests. Resolve tenant/student identity server-side; do not trust caller-supplied ownership IDs.
6. Keep migrations explicit and operator-controlled; ordinary API startup must not apply schema changes.
7. Run proportionate safe checks. Report skipped tests and unresolved database state; update existing canonical docs when behavior or schema contracts change.
