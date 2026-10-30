# ADR 0005: Preserve enrollment history as dated records

- **Status:** Core behavior accepted by the project brief; detailed status/date/constraint rules are proposed.
- **Date:** 2026-10-30

## Context

Students may be promoted or transferred between classes. Attendance and daily reports must continue to identify the class context that applied when the record was created. Replacing a student's current class would erase historical meaning.

## Decision

Represent each class assignment as a `StudentEnrollment` row with school, student, class, start date, exclusive end date, status, and audit timestamps/actors as needed. A transfer is one database transaction that closes the prior row and creates the next row; do not delete or overwrite history.

Attendance and daily reports reference `EnrollmentId`. Use half-open `[StartDate, EndDate)` semantics and reject overlapping enrollment periods for the same student. Prefer a PostgreSQL exclusion constraint (potentially with `btree_gist`) plus application validation; confirm the extension/DDL and status semantics before implementation.

## Alternatives considered

- Keep only a current `ClassId` on Student: rejected because history and historical class context are lost.
- Update the prior enrollment to point to a new class: rejected because old attendance/report relationships become misleading.
- Create multiple rows but allow overlapping dates: rejected because a student could be simultaneously assigned to conflicting classes without a business rule.
- Application-only duplicate checks: insufficient for concurrent transfers; a database constraint is recommended.

## Consequences and trade-offs

- Historical class context is stable for attendance, reports, and audit.
- Transfers must be transactional and concurrent-write safe.
- PostgreSQL-specific range/exclusion constraints add migration complexity; exact statuses, same-day moves, retroactive corrections, and temporary gaps require approval.
- A student can have no current enrollment during gaps; parent profile/history behavior during that period must be defined.

## References

- [EF Core indexes and unique constraints](https://learn.microsoft.com/en-us/ef/core/modeling/indexes)
- [Npgsql date/time handling](https://www.npgsql.org/doc/types/datetime.html)
- [PostgreSQL range types and exclusion constraints](https://www.postgresql.org/docs/current/rangetypes.html)
