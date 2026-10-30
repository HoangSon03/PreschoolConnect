# Proposed Database ERD

**Status:** conceptual model for architecture review; not a migration or final schema. IDs are proposed UUIDs. Use PostgreSQL/EF Core constraints as well as application rules. All school-owned rows carry `SchoolId`; the composite keys below make school ownership part of relationships, not just a query convention.

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
    string StudentCode
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
    string Status
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
    string EntityId
    datetime OccurredAt
    json Metadata
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
- `Student`: unique `(SchoolId, StudentId)` and `(SchoolId, StudentCode)`.
- `StudentAccount`: unique `StudentId` and unique `UserId` (one user maps to at most one student globally); composite FKs `(SchoolId, StudentId)` and `(SchoolId, UserId)` point to the same-school student and membership. Require the linked membership to have `Parent` role in the application and test it.
- `SchoolYear`: `EndDate > StartDate`; school-local date policy must be settled. `Classroom`: composite FK `(SchoolId, SchoolYearId)`; recommended unique `(SchoolYearId, Name)`.
- `ClassTeacher`: composite FKs to the same-school class and membership; unique `(SchoolId, ClassId, UserId)`.
- `StudentEnrollment`: composite FKs to same-school student and class; `EndDate` is exclusive and must be after `StartDate`. Transfers close the prior enrollment and create the next inside one transaction. Enforce non-overlapping student date ranges in PostgreSQL (a GiST exclusion constraint with `btree_gist` is a candidate) and validate the same invariant in application use cases. Confirm required extension/constraint approach before schema work.
- `AttendanceRecord`: unique `(EnrollmentId, AttendanceDate)`; status restricted to the MVP attendance set; checkout cannot precede check-in; date must fall within the enrollment's effective interval.
- `DailyReport`: unique `(EnrollmentId, ReportDate)`; `NapDurationMinutes >= 0`; parents can read published records only. Published corrections require explicit actor/time audit.
- `Announcement`: one `TargetType` (`School`, `Class`, `Student`) and matching target rows only; state/timestamps must be consistent. `CopyOfAnnouncementId` references the original without mutating it.
- `AnnouncementStudentTarget`: unique `(SchoolId, AnnouncementId)` so a student-targeted announcement selects exactly one student. `AnnouncementClassTarget` supports multiple classes. A school target has no rows in either target table. The cross-table relationship between `TargetType` and target rows needs one transactional publication/use-case path and tests; a row-level `CHECK` cannot enforce it by itself.
- `AnnouncementRecipient`: unique `(AnnouncementId, StudentId)`; rows are immutable membership snapshots except for `ReadAt`. All target and recipient relations include `SchoolId` in composite FKs.
- `AuditLog`: store a minimal allowlisted record; do not put passwords, tokens, full report text, or unnecessary child/parent data in metadata.

Proposed index strategy: indexes for tenant-leading lookup paths and common access patterns (for example `(SchoolId, AttendanceDate)`, `(SchoolId, StudentId, StartDate)`, `(SchoolId, Status)` only where measured). Unique and composite FK constraints are correctness controls, not just performance indexes. Review query plans before adding speculative indexes.

`School.TimeZoneId` is a proposed addition because calendar dates and instants have different semantics. Store school business dates as PostgreSQL `date`; store instants such as publication/check-in times in UTC `timestamptz`. Confirm the school time-zone and daylight-saving policy first.
