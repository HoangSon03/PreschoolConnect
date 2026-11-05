# Proposed Database ERD

**Status:** conceptual model for architecture review; not a migration or final schema. IDs are proposed UUIDs. Use PostgreSQL/EF Core constraints as well as application rules. All school-owned rows carry `SchoolId`; the composite keys below make school ownership part of relationships, not just a query convention.

**Implemented audit overlay (2026-11-03):** School, SchoolMembership, Student,
StudentAccount, SchoolYear and Classroom each have `CreatedAtUtc timestamptz`,
nullable `CreatedByUserId uuid`, `LastAction varchar(1)`, nullable
`LastActionByUserId uuid`, and `LastActionAtUtc timestamptz`. Both user references
are restrictive Identity FKs; CHECK constraints restrict I/U/D and timestamp
ordering. LastAction D is retained but excluded by the named EF SoftDelete filter.
The four older CreatedUtc columns remain immutable compatibility fields.
Identity, permission joins and append-only AuditLogs do not use this filter.
This does not imply the future conceptual modules below are implemented.

**Implemented Teacher extension:** `Teachers` adds a school-owned audited
personnel profile with nullable restrictive Identity UserId, UI personal/contact,
qualification/work fields and IsActive. A unique (SchoolId, UserId) index applies
to linked users, including retained rows. `TeacherAssignments` adds audited
SchoolId/TeacherId/ClassroomId/Role/StartedAtUtc/EndedAtUtc rows; composite
same-school foreign keys target Teachers and Classrooms. Active teacher/class
pairs are unique; ending assignments retains rows. Both tables reuse immutable
creation metadata, append-only AuditLogs, soft-delete filters and PostgreSQL
no-physical-delete guards. This is the implemented counterpart of conceptual
CLASS_TEACHER below, not timetable or document storage.

**Implemented Student extension:** `Student.Id` is also its displayed code.
The existing Student table retains FirstName/LastName/DateOfBirth and adds
Gender/Address, guardian/relationship/phone/email/pickup/emergency, nullable
Height/Weight and optional Care. No Parents table or new Identity model.
`StudentInClass` (`StudentInClasses`) is the implemented enrollment counterpart
below: audited UUID/SchoolId/StudentId/ClassroomId/StartDate/EndDate/AgeGroup/
StudyMode, with restrictive same-school composite FKs. EndDate is required,
exclusive and initially the year-end plus one day. The approved bundled
`btree_gist` exclusion constraint rejects overlapping periods for each
school/student, including concurrent inserts. History can only be shortened to
today while effective; no physical/soft deletion or reopening. Retained
enrollments block deletion of their student and class.

```mermaid
erDiagram
  APP_USER {
    uuid UserId PK
    string UserName UK
    string Email
    bool IsActive
  }
  SCHOOL {
    uuid SchoolId PK
    string Name
    string TimeZoneId
    bool IsActive
  }
  SCHOOL_MEMBERSHIP {
    uuid MembershipId PK
    uuid SchoolId FK
    uuid UserId FK
    string Status
  }
  MEMBERSHIP_ROLE {
    uuid MembershipId PK, FK
    string Role PK
  }
  STUDENT {
    uuid StudentId PK
    uuid SchoolId FK
    string FullName
    date DateOfBirth
    string ParentContactName
    string ParentContactPhone
    string ParentContactEmail
    bool IsActive
  }
  STUDENT_ACCOUNT {
    uuid StudentAccountId PK
    uuid SchoolId FK
    uuid StudentId FK, UK
    uuid UserId FK, UK
  }
  SCHOOL_YEAR {
    uuid SchoolYearId PK
    uuid SchoolId FK
    string Name
    date StartDate
    date EndDate
  }
  CLASSROOM {
    uuid ClassId PK
    uuid SchoolId FK
    uuid SchoolYearId FK
    string Name
  }
  CLASS_TEACHER {
    uuid SchoolId PK, FK
    uuid ClassId PK, FK
    uuid UserId PK, FK
  }
  STUDENT_ENROLLMENT {
    uuid EnrollmentId PK
    uuid SchoolId FK
    uuid StudentId FK
    uuid ClassId FK
    date StartDate
    date EndDate
    string AgeGroup
    string StudyMode
  }
  ATTENDANCE_RECORD {
    uuid AttendanceId PK
    uuid SchoolId FK
    uuid EnrollmentId FK
    date AttendanceDate
    string Status
    datetime CheckInAt
    datetime CheckOutAt
    string Note
    uuid RecordedByUserId FK
  }
  DAILY_REPORT {
    uuid DailyReportId PK
    uuid SchoolId FK
    uuid EnrollmentId FK
    date ReportDate
    string MealNote
    int NapDurationMinutes
    string Mood
    string ActivityNote
    string HealthNote
    string Status
    datetime PublishedAt
    uuid CreatedByUserId FK
  }
  ANNOUNCEMENT {
    uuid AnnouncementId PK
    uuid SchoolId FK
    uuid CreatedByUserId FK
    string TargetType
    string Status
    string Title
    string Content
    datetime CreatedAt
    datetime PublishedAt
    uuid CopyOfAnnouncementId FK
  }
  ANNOUNCEMENT_CLASS_TARGET {
    uuid SchoolId PK, FK
    uuid AnnouncementId PK, FK
    uuid ClassId PK, FK
  }
  ANNOUNCEMENT_STUDENT_TARGET {
    uuid SchoolId PK, FK
    uuid AnnouncementId PK, FK
    uuid StudentId PK, FK
  }
  ANNOUNCEMENT_RECIPIENT {
    uuid SchoolId PK, FK
    uuid AnnouncementId PK, FK
    uuid StudentId PK, FK
    datetime ReadAt
  }
  AUDIT_LOG {
    uuid AuditLogId PK
    uuid SchoolId FK
    uuid ActorUserId FK
    string Action
    string EntityType
    uuid EntityId
    datetime OccurredAtUtc
    string SystemOperation
    json ChangedFields
  }

  APP_USER ||--o{ SCHOOL_MEMBERSHIP : joins
  SCHOOL ||--o{ SCHOOL_MEMBERSHIP : has
  SCHOOL_MEMBERSHIP ||--o{ MEMBERSHIP_ROLE : grants
  SCHOOL ||--o{ STUDENT : owns
  SCHOOL_MEMBERSHIP ||--o| STUDENT_ACCOUNT : may_map
  STUDENT ||--o| STUDENT_ACCOUNT : has
  SCHOOL ||--o{ SCHOOL_YEAR : defines
  SCHOOL_YEAR ||--o{ CLASSROOM : contains
  CLASSROOM ||--o{ CLASS_TEACHER : assigned
  SCHOOL_MEMBERSHIP ||--o{ CLASS_TEACHER : teaches
  STUDENT ||--o{ STUDENT_ENROLLMENT : history
  CLASSROOM ||--o{ STUDENT_ENROLLMENT : receives
  STUDENT_ENROLLMENT ||--o{ ATTENDANCE_RECORD : records
  STUDENT_ENROLLMENT ||--o{ DAILY_REPORT : reports
  APP_USER ||--o{ ATTENDANCE_RECORD : records
  APP_USER ||--o{ DAILY_REPORT : creates
  SCHOOL ||--o{ ANNOUNCEMENT : owns
  APP_USER ||--o{ ANNOUNCEMENT : authors
  ANNOUNCEMENT ||--o{ ANNOUNCEMENT_CLASS_TARGET : targets
  CLASSROOM ||--o{ ANNOUNCEMENT_CLASS_TARGET : selected
  ANNOUNCEMENT ||--o| ANNOUNCEMENT_STUDENT_TARGET : targets
  STUDENT ||--o{ ANNOUNCEMENT_STUDENT_TARGET : selected
  ANNOUNCEMENT ||--o{ ANNOUNCEMENT_RECIPIENT : snapshots
  STUDENT ||--o{ ANNOUNCEMENT_RECIPIENT : receives
  ANNOUNCEMENT ||--o| ANNOUNCEMENT : copied_from
  APP_USER ||--o{ AUDIT_LOG : acts
  SCHOOL ||--o{ AUDIT_LOG : scopes
```

## Keys and constraints to implement and test

- Identity user names/emails follow Identity's configured normalization and uniqueness rules.
- `SchoolMembership`: unique `(SchoolId, UserId)` and a composite key usable by child FKs. Role assignments are unique `(MembershipId, Role)`. Roles: `SchoolAdmin`, `Teacher`, `Parent`; do not rely on global roles for school authorization.
- `Student`: implemented composite unique `(SchoolId, Id)`; its immutable `Id`
  is the displayed student code, with no separate StudentCode column.
- `StudentAccount`: unique `StudentId` and unique `UserId` (one user maps to at most one student globally); composite FKs `(SchoolId, StudentId)` and `(SchoolId, UserId)` point to the same-school student and membership. Require the linked membership to have `Parent` role in the application and test it.
- `SchoolYear`: `EndDate > StartDate`; school-local date policy must be settled.
  `Classroom`: composite FK `(SchoolId, SchoolYearId)`. Approved 2026-11-04:
  immutable original SchoolYearId and active-only, trimmed, case-insensitive
  name uniqueness per school/year, including Vietnamese letters. Migration
  `20261104080000_ClassroomIntegrity` adds the `pg_c_utf8` expression index and
  year-immutability trigger without rewriting business records.
- Implemented school-year integrity (approved 2026-11-03): a per-school unique
  expression index on trimmed, lowercased names uses PostgreSQL 17's built-in
  `pg_c_utf8` collation for Unicode case folding even in a `C`-locale database.
  SQL triggers reject inclusive overlapping date ranges, date changes/deletion
  when classes exist, and changes to year identity/ownership. Year and class
  writes share a per-school transaction advisory lock to serialize integrity
  checks under concurrent writers. The existing class/year FK remains restrictive.
  No extension, package, new persisted state or provider is added. These SQL
  guards are migration-defined; they are not EF model annotations.
- `20261103032915_StandardizedAuditing` adds row metadata and AuditLogs without
  recreating business tables. Its partial school-year name index and overlap guard
  consider LastAction other than D; retained classes, including tombstones, still
  lock year dates/deletion. The classroom unique name index is active-only.
  School slug, membership identity, and student-account identity uniqueness remain
  all-record rules. Membership-role parent deletion becomes restrictive instead
  of cascading; student-account Identity and all audit actor FKs are restrictive.
  Database guards forbid physical business deletion and AuditLogs mutation.
  Safe unknown-actor backfill and forward-only recovery are documented in
  [migration notes](./audit-migration-notes.md); see
  [entity deletion/history policies](./audit-and-soft-delete.md).
- `20261103030000_SchoolYearIntegrity` first checks old names/date ranges and
  refuses conflicting data without rewriting it. An operator must resolve legacy
  conflicts deliberately before applying it. If migrations remain pending,
  year/class mutations return 503 rather than bypassing database guards. The
  application's existing development startup still logs degraded initialization;
  that does not mean migrations succeeded.
  See [PostgreSQL 17 collations](https://www.postgresql.org/docs/17/collation.html)
  and [advisory locks](https://www.postgresql.org/docs/17/explicit-locking.html#ADVISORY-LOCKS).
- `ClassTeacher`: composite FKs to the same-school class and membership; unique `(SchoolId, ClassId, UserId)`.
- `StudentInClass`: implemented counterpart of conceptual `StudentEnrollment`;
  composite same-school FKs, `EndDate > StartDate`, and approved `btree_gist`
  exclusion constraint over `(SchoolId, StudentId, daterange(StartDate, EndDate, '[)'))`.
  Transfers close the prior enrollment and create the next inside one transaction.
  New periods are bounded by the class's non-deleted year. Application validation,
  audit concurrency tokens and SQL history/dependency guards complement the
  overlap constraint. See [approved enrollment rules](./business-rules.md#enrollment-history).
- `AttendanceRecord`: unique `(EnrollmentId, AttendanceDate)`; status restricted to the MVP attendance set; checkout cannot precede check-in; date must fall within the enrollment's effective interval.
- `DailyReport`: unique `(EnrollmentId, ReportDate)`; `NapDurationMinutes >= 0`; parents can read published records only. Published corrections require explicit actor/time audit.
- `Announcement`: one `TargetType` (`School`, `Class`, `Student`) and matching target rows only; state/timestamps must be consistent. `CopyOfAnnouncementId` references the original without mutating it.
- `AnnouncementStudentTarget`: unique `(SchoolId, AnnouncementId)` so a student-targeted announcement selects exactly one student. `AnnouncementClassTarget` supports multiple classes. A school target has no rows in either target table. The cross-table relationship between `TargetType` and target rows needs one transactional publication/use-case path and tests; a row-level `CHECK` cannot enforce it by itself.
- `AnnouncementRecipient`: unique `(AnnouncementId, StudentId)`; rows are immutable membership snapshots except for `ReadAt`. All target and recipient relations include `SchoolId` in composite FKs.
- `AuditLog`: store a minimal allowlisted record; do not put passwords, tokens, full report text, or unnecessary child/parent data in metadata.

Proposed index strategy: indexes for tenant-leading lookup paths and common access patterns (for example `(SchoolId, AttendanceDate)`, `(SchoolId, StudentId, StartDate)`, `(SchoolId, Status)` only where measured). Unique and composite FK constraints are correctness controls, not just performance indexes. Review query plans before adding speculative indexes.

`School.TimeZoneId` is a proposed addition because calendar dates and instants have different semantics. Store school business dates as PostgreSQL `date`; store instants such as publication/check-in times in UTC `timestamptz`. Confirm the school time-zone and daylight-saving policy first.
