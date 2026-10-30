# ADR 0001: PostgreSQL as the database

- **Status:** Accepted by the project brief; implementation is pending.
- **Date:** 2026-10-30

## Context

Preschool Connect stores sensitive school, student, attendance, report, and announcement data. The brief requires the same database provider in local development and production, and explicitly selects PostgreSQL.

## Decision

Use PostgreSQL for local development, tests that require relational behavior, staging, and production. Use EF Core with the Npgsql provider for persistence. Do not substitute SQLite or an in-memory provider for production-like integration tests.

## Alternatives considered

- SQLite locally and PostgreSQL in production: rejected because provider differences can conceal migration, constraint, type, and query issues.
- SQL Server or another relational database: not selected; no requirement justifies changing the specified provider.
- MongoDB/document database: rejected for the relational school/student/enrollment/recipient constraints and no concrete document-model need.

## Consequences and trade-offs

- Local and production database behavior stays aligned, especially for composite keys, exclusion constraints, date types, and transactions.
- Development requires PostgreSQL tooling/server setup; none of `psql`, `pg_ctl`, or `postgres` was found on the current PATH during Milestone 00.
- PostgreSQL-specific features may require explicit migration SQL and database-level tests. Avoid claiming provider portability.
- Keep secrets in environment/secret stores, not source control. Plan backup, restore, upgrades, and least-privilege database roles before a pilot.

## References

- [Npgsql EF Core provider](https://www.npgsql.org/efcore/)
- [Npgsql EF Core 10.0 release notes](https://www.npgsql.org/efcore/release-notes/10.0.html)
- [PostgreSQL row security](https://www.postgresql.org/docs/current/ddl-rowsecurity.html)
