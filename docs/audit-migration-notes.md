# Audit migration notes

## Teacher profile extension

`20261104095259_TeacherProfiles` follows ClassroomIntegrity. It adds Teachers
and TeacherAssignments, the (SchoolId, Id) classroom alternate key for
same-school assignment FKs, linked-user and active-assignment uniqueness, audit
checks/FKs and guards. Existing business tables/data are not recreated.
PostgreSQL reuses `guard_business_audit()` and adds teacher membership,
immutable-linked-identity and append-only-assignment-history checks.
The migration is forward-only. It does not create teachers, accounts,
credentials or assignments automatically. Apply only as an explicit operator
action on a verified target; normal startup never applies it.

## Classroom integrity extension - 2026-11-04

`20261104080000_ClassroomIntegrity` follows StandardizedAuditing. It adds
`UX_Classrooms_NormalizedName`, an active-only unique expression index over
SchoolId, SchoolYearId and `lower(btrim(Name) COLLATE "pg_c_utf8")`, plus a trigger
that rejects changing a class's original SchoolYearId. The existing composite
foreign key, exact-name index, audit guards and active-year guards remain.
These are migration-defined SQL, so no EF model/snapshot changes are required.

The migration checks for conflicting active names first and stops without
rewriting or deleting records. An authorized operator must resolve legacy
conflicts deliberately before retrying. It never moves classes or fabricates
history. Run the operator checklist below; ordinary startup does not migrate.
The application database has not been inspected or migrated for this delivery.

New tests verify generated SQL without a connection, and exercise concurrent
Vietnamese-case duplicates, active-only name reuse, immutable year references
and refusal of legacy conflicts on the existing disposable PostgreSQL harness.
On 2026-11-04, the focused Classroom/SchoolYear/audit suite passed all 35 tests
on a newly initialized loopback-only PostgreSQL 17.11 cluster in session storage.
That cluster was shut down and removed afterwards; no application database was
used. This does not establish migration state in any deployment.

## Schema impact

`20261103032915_StandardizedAuditing` follows the existing
`20261103030000_SchoolYearIntegrity` migration. Existing migrations are unchanged.
The generated EF designer/snapshot include six tables' metadata, actor foreign
keys/indexes, append-only AuditLogs, the active-only classroom unique index, and
restrictive membership-grant and student-account user relationships. Expression
indexes and trigger functions remain migration-defined SQL, as before.

No business table/data is dropped or recreated. This migration is **forward-only**:
Down refuses to run before schema changes because downgrading would discard audit
history and make tombstones appear active. Use a reviewed forward corrective
migration; a normal downgrade is not a restore or purge mechanism.

## Safe baseline

- School, SchoolMembership, Student, StudentAccount: preserve existing CreatedUtc
  as CreatedAtUtc; assume the existing timestamp is the best available creation
  evidence. Keep the original compatibility column.
- SchoolYear and Classroom: no historical creation timestamp exists; use one
  transaction timestamp as the documented backfill baseline, not an invented
  original creation date.
- All existing rows start with LastAction I as an **initial legacy baseline**,
  not proof that a historical insertion event was recorded.
- CreatedByUserId and LastActionByUserId remain null. Do not infer creators from
  current memberships, student-account ownership, or administrator accounts.
- LastActionAtUtc is the migration timestamp (or the old creation timestamp if
  that is later); this explicitly avoids asserting a known historical update.
- No synthetic historical AuditLogs are created.
- Temporary ADD COLUMN defaults are removed after backfill. Runtime writes must
  supply real audit metadata; missing/empty defaults cannot silently succeed.

The migration stops, with no automatic cleanup, for orphan Identity mappings or
cross-school student-account links. The preceding year-integrity migration also
stops for existing conflicting names/ranges. Foreign keys and CHECK constraints
validate the resulting data before commit. Resolve legacy errors deliberately;
never invent users, discard rows, or relax tenant constraints to get past them.

## Operator checklist

1. Back up and verify restore procedures using approved tools/permissions.
2. Inspect migration history and schema only with authorized access. Review
   exceptional timestamps and invalid relationships without copying personal
   records into logs or agent context.
3. Schedule a maintenance window: backfill and index replacement take table locks;
   duration depends on row counts. No concurrent-index or zero-downtime guarantee
   is made for this transactional EF migration.
4. Apply forward migrations as an explicit operator action:

   ```powershell
   dotnet run --no-launch-profile --project .\server\PreschoolConnect.Api\PreschoolConnect.Api.csproj -- --migrate
   ```

   Configure `ConnectionStrings:MigrationConnection` for a migration-only role
   when available; the command falls back to `ConnectionStrings:DefaultConnection`
   for local development. Ordinary API startup never applies migrations.
   Do not alter applied migrations or automatically repair legacy conflicts.
5. Verify migration history, constraints/index predicates, API readiness, and a
   synthetic authorized insert/update/delete. Pending migrations continue to
   block tenant resolution and real year/class writes with 503 before new audit
   columns are queried.

The ordinary loopback database's applied migration history has not been verified
by this remediation. Do not start it or infer its schema from source files. The
explicit migration command must be run only after an authorized operator
confirms the target, backup, migration history, and legacy-data checks.

## Tests

Focused auditing tests cover sync/async metadata, UTC, preservation, explicit
system and authenticated actors, missing/cross-school actors, filters, D-update
rejection, detached/metadata tampering, retained relationships, membership
authorization, admin reads, and client overposting.

Optional PostgreSQL tests use the existing loopback-only disposable database
harness. `PRESCHOOL_TEST_POSTGRES` must point to a **separate synthetic server**
with maintenance database `postgres`, host `127.0.0.1`, and a nonprivileged port.
Tests create randomly named databases and drop only those test databases.

```powershell
dotnet test .\server\PreschoolConnect.Tests\PreschoolConnect.Tests.csproj --no-restore --filter "FullyQualifiedName~Audit"
```

With the test-only connection configured, the full backend suite additionally
verifies new/legacy migrations, safe backfill, CHECK/FK constraints, active-only
uniqueness, retained account exclusivity, append-only events, raw/bulk bypass
guards, stale writes, and concurrent year/class integrity. Without that variable,
PostgreSQL tests are explicitly skipped, not counted as verified constraints.

Use the existing [verification script](../scripts/verify.ps1) for the full
backend/frontend checks. No new dependencies or system tools were adopted.
