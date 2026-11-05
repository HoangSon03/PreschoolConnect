# Audit and soft-delete policy

Implemented for the existing business model on 2026-11-03. This policy supersedes
physical deletion of unused school years. Teacher and Student delivery extend
this coverage; attendance, reporting and announcements remain separate modules.

## Coverage and deletion rules

| Entity | Row auditing and soft delete | Deletion policy |
|---|---|---|
| School | Yes | Reject while any business dependents exist, including tombstones. Usually deactivate instead. Audit events remain attached. |
| SchoolYear | Yes | Reject when any classroom references it, including deleted classes. Referenced years cannot change dates. |
| Classroom | Yes | Keep its original year relation immutable; reject deletion while any StudentInClass history exists. |
| Student | Yes | Reject while a student-account mapping or enrollment history exists, including retained rows. |
| StudentInClass | Yes, no delete operation | End by shortening EndDate to today, never deleting. Student/class/start/age-group/study-mode and already-ended history are immutable. Re-enrollment creates a new row; year-end expiration preserves the original boundary. |
| SchoolMembership | Yes | Keep grants and Identity references. Deleted membership cannot authorize requests. |
| StudentAccount | Yes | Preserve user/student identity and uniqueness permanently; no implicit remapping or reuse. |
| Teacher | Yes | Retain personnel and linked Identity history; no profile-delete endpoint. Linked users cannot be reassigned. |
| TeacherAssignment | Yes | End by setting EndedAtUtc with an audited U operation, never removal. Closed assignment identity/role/start/end are immutable; re-assignment creates a new row. |
| SchoolMembershipRole | No soft-delete base | A composite-key authorization join, not a separately archived business object. Explicit grant/revoke events are logged; deletion of its membership does not delete grants. Authorization always starts from an active, non-deleted membership. |
| ASP.NET Core Identity tables | No business filters | Preserve framework lifecycle and account deactivation. Business/audit user references use restrictive foreign keys. |
| AuditLog | Append-only events | No update, deletion, or soft-delete filter. Separate from latest-row metadata. |
| Proposed attendance, reports, announcements, recipient snapshots | Not implemented | Define entity-specific lifecycle before adding them; never cascade archival into historical records or recipient snapshots. |

Restoration and purge are **not supported**. Normal updates and repeated deletions
of D records are rejected. New identity/permission provisioning and any future
restore/purge workflow require an explicit authorization and retention design.

## Row metadata

`BaseAuditableEntity` supplies:

| Field | PostgreSQL | Behavior |
|---|---|---|
| CreatedAtUtc | timestamp with time zone, not null | Set once on insert; immutable. |
| CreatedByUserId | uuid, nullable, restricted Identity FK | Authenticated creator; null only for named system work or unknown legacy history. |
| LastAction | varchar(1), not null, CHECK I/U/D | I = insert, U = update, D = soft delete. |
| LastActionByUserId | uuid, nullable, restricted Identity FK | Latest authenticated actor, or null for system/legacy baseline. |
| LastActionAtUtc | timestamp with time zone, not null | Latest operation; at least creation time, advances on writes. |

Npgsql stores instants in `timestamptz`; its displayed timezone depends on the
database session, not the stored instant. Application timestamps have zero UTC
offset and PostgreSQL-compatible microsecond precision. Creation metadata is
preserved on U/D. `CreatedUtc` remains an immutable compatibility column on the
four tables that previously had it; new inserts set it to CreatedAtUtc.

Private metadata setters and dedicated request DTOs prevent overposting. Current
year/class request DTOs reject unmapped fields, including audit and ownership IDs.
LastAction and LastActionAtUtc are optimistic concurrency tokens. A stale writer
cannot resurrect a tombstone. Detached updates must load trustworthy metadata
first; blind `Update(new Entity { Id = ... })` is rejected.

## Central persistence and actor policy

`ApplicationDbContext` handles both sync and async SaveChanges overloads:

1. Detect and validate all business changes and permission-grant changes.
2. Resolve the request's Identity actor, or a named system operation.
3. Require an active account/membership in each written school for user actors.
   This is defense in depth, **not a replacement for endpoint role authorization**.
4. Stamp inserts and updates, convert Deleted to a metadata-only UPDATE, and
   append minimal AuditLogs in the same EF transaction.
5. Restore prepared metadata/log entries if persistence fails, so retry does not
   fabricate historical events.

EF dependent processing is deferred until SaveChanges so a tracked, required
relationship is not severed before business deletion becomes a soft update.
Identity retains its framework cascade behavior; business parents and audit/user
references remain restrictive.

`RequestAuditActorProvider` reads only the authenticated principal. Without an
HTTP request, callers must explicitly open a scoped
`AuditActorContext.BeginSystemOperation("job-name")`. Development initialization
uses `development-initialization`; system operations store null user IDs and put
their operation name in AuditLogs. Missing actor configuration fails explicitly.
System scope is scoped DI state, disposed after use, and cannot turn an
unauthenticated HTTP request into a system actor.

Directly constructed contexts need an explicit IAuditActorProvider for business
writes. Identity-only writes and reads need no fabricated actor. Callers using
`SaveChanges(false)` must accept tracker changes explicitly after their transaction
commits before submitting another save, as with the normal EF pattern.

## Queries and history

The named EF `SoftDelete` filter excludes LastAction D on covered business types.
It is not tenant authorization. Normal APIs explicitly select the resolved school.
Do not use cached `Find` results to decide whether a record is active.

Implemented SchoolAdmin-only, school-scoped reads:

- `/api/admin/school-years/deleted`: up to 200 most recent deleted year summaries.
- `/api/admin/deleted-records/{entityType}?page=1`: audit metadata only, 100 rows
  per page. Types: `schools`, `school-years`, `classrooms`, `students`,
  `memberships`, `student-accounts`.
- `/api/admin/audit-logs?page=1`: 100 minimal historical events per page.

No platform-admin role exists. A deleted school cannot be authorized through a
deleted membership; cross-school/offline archival inspection requires a reviewed
operator path, not an API bypass.

Deleted classes still count as retained year dependencies. Those counts use a
separate explicitly scoped query: putting IgnoreQueryFilters in a nested LINQ
query can also suppress filters on its outer query.

When historical modules are implemented, query their retained fact/snapshot rows
directly and deliberately resolve archived parents with explicit tenant/student
authorization. Do not put parent-active filters on attendance, enrollment history,
reporting facts, or announcement recipient snapshots. Required-navigation joins
must be tested because a filtered parent can otherwise hide a historical child.

Student history queries explicitly join school-scoped retained class/year names
for Admin and the mapped Parent. Teacher queries include only current eligible
enrollments and explicitly non-deleted assignments/teachers/classes/years; filter
bypass never grants historical other-class access. AuditLogs contain allowlisted
changed-field names only, never Student health/care or contact values. Measurement
and care changes still stamp row metadata and emit an event without health content.

## PostgreSQL integrity and bypass paths

- CHECK constraints restrict I/U/D and timestamp ordering.
- Audit actor FKs are nullable and restrictive, never cascading or SET NULL.
- School slugs, membership school/user pairs, and student-account user/student
  mappings stay unique across all records.
- Classroom names are unique within a year among non-deleted classes, ignoring
  trimmed-name casing (including Vietnamese letters). The migration-defined
  `UX_Classrooms_NormalizedName` index handles concurrent inserts; EF and a
  PostgreSQL trigger reject changes to the original SchoolYear association.
- Unicode-normalized school-year names and inclusive date overlaps apply only
  among non-deleted years.
- Physical business DELETE, changes to creation/identity metadata, normal updates
  to D, and updates without advancing audit state are rejected by triggers.
- School/year/student dependency checks preserve retained records. Class writes
  require a non-deleted same-school year; account mappings require a non-deleted
  same-school student. School-scoped audit locks and existing year/class locks
  serialize deletion/attachment and year-integrity checks.
- AuditLogs UPDATE/DELETE is rejected by both EF and PostgreSQL.

Runtime code currently contains **no ExecuteDelete, ExecuteUpdate, raw SQL writes,
or external bulk writers**. SQL writes exist in migrations and synthetic
constraint tests only. These bypass SaveChanges and are not supported runtime
mutation APIs. Database triggers reject physical deletion or unstamped updates,
but SQL deliberately supplying valid metadata can still bypass EF event creation
and request authorization. Such writes require an explicitly reviewed,
actor-aware audited path; do not claim SQL access is tenant-safe. Restrict database
credentials and never give runtime/agent users migration-level privileges.

## AuditLogs minimization

Every covered entity insert/update/soft delete and every membership role grant/
revoke emits an event: school ID, entity type/ID, I/U/D, actor or system operation,
UTC time, and changed **property names**, never their values. Events for inserts/
deletes have an empty changed-field list; grant events identify `Role` without
copying the grant value. Entity types come from the existing model; changed
properties must belong to the persistence layer's explicit business-field
allowlist and actually differ from the original value. New fields require
deliberate opt-in, not client-supplied log text. EF creation metadata is never
included as a changed business field.

Future high-impact actions requiring explicit events include account lifecycle/
permission changes, enrollment transfers, attendance corrections, report
publication/corrections, and announcement publication. Those workflows are not
implemented here. Do not log password/token material, health/report text,
contact details, or full before/after snapshots. Audit retention/export/purge and
legal access processes still require approval before real child data is used.

## Migration and verification

See [audit migration notes](./audit-migration-notes.md) for safe backfill,
forward-only rollback policy, operational limits, and test commands.
