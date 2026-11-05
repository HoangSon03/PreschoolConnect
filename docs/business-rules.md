# Business Rules

**Authority:** explicit rules in the project brief are confirmed. Rules labelled **Recommended** are defaults for review, not new confirmed requirements.

## Identity, school membership, and students

- Identity authenticates a user. An active `SchoolMembership` authorizes that user in one school; membership roles are scoped to that school.
- A user may belong to multiple schools; the server must validate the membership for each request. **Recommended:** membership may hold multiple role assignments so staff can hold more than one role in one school.
- No self-registration. Administrators provision and activate accounts. The first-login credential/setup handoff must be secure and verifiable; the delivery method is unresolved.
- One dedicated student account maps to one student, and a student has at most one dedicated student account. Student parent/guardian contact fields remain on `Student`; there is no separate `Parents` entity.
- Parent-facing APIs resolve the student from the authenticated account mapping. Never accept a client `StudentId` as proof of access.
- **Approved Student delivery:** the displayed student code is the record's
  immutable UUID `Id`; no separate code column, prefix or sequential counter.

## School years, classes, and teacher assignments

- Each school year and class belongs to exactly one school. Each class belongs to exactly one school year.
- A teacher may be assigned to multiple classes. A class may have multiple assigned teachers.
- Validate all teacher/class/student relationships within the same school. Account status and active membership are checked at request time.
- **Approved 2026-11-03:** school-year names are unique within a school after
  trimming and case folding (including Vietnamese letters) among non-deleted
  years. Their date ranges do not overlap, including endpoints; the next year
  starts after the previous end.
  End date must be strictly after start date.
- Only SchoolAdmin of the resolved school may add/edit/delete school years.
  A year without classes may be renamed, have its dates edited, or be deleted
  after confirmation by setting LastAction D, never physical deletion. Once it
  has classes (including deleted classes), only its name may change; dates and
  deletion are blocked server-side. Never cascade-delete classes or automatically
  move them to another year.
- School-year phases in the UI are calculated from the date range, not a new
  persisted active/archive status or a default-year policy. Years are not
  automatically created.
- **Approved 2026-11-04:** classroom names are trimmed, required, at most
  100 characters, and unique within a school year among non-deleted classes,
  ignoring case (including Vietnamese letters). A deleted class does not reserve
  its name. The original SchoolYear association is immutable.
- Classroom delivery is limited to the existing real name/year create, list,
  search/year filter and basic-information dialog. Preview age groups, rooms,
  capacity, planned start dates, notes, rosters and teacher assignments are not
  approved backend requirements for this delivery. Edit/delete APIs are not
  included in that delivery. The subsequent Teacher delivery below adds assignments.

### Approved Teacher delivery

- SchoolAdmin manages teacher personnel profiles only within the resolved school.
  Teacher can read only their explicitly linked profile and currently assigned,
  non-deleted classes; Parent cannot access personnel APIs. Ending an assignment,
  pausing a teacher profile, revoking membership or disabling the account removes
  applicable access on the next request.
- Profiles contain the existing UI's personal/contact, qualification and work
  fields. Required fields, bounded text, ten-digit phone, email, non-future
  birthday, graduation year and nonnegative integer experience are validated.
  Employment status is independent of Identity account activation.
- Creating a profile never creates credentials or matches accounts by email.
  Admin may explicitly link an active user with Teacher membership in the same
  school. A linked identity cannot be reassigned; one profile/user/school is
  retained, including tombstones. Codes use `GV-` plus the profile UUID.
- A teacher can have several classes and a class several teachers. Assignment
  roles are the existing UI's primary/co-teacher labels, not additional JWT roles.
  Assignment starts immediately at server UTC; ending it stamps EndedAtUtc,
  retains history and allows later re-assignment as a new row. No future scheduling.
  Duplicate active teacher/class pairs are rejected, including concurrent writes.
- Teacher profile and assignment rows use the shared audit/soft-delete pipeline;
  no physical-delete or profile-delete API is exposed. Admin can read retained
  assignment history; Teacher responses omit ended or inaccessible assignments.
- This delivery excludes timetable management and binary document storage by
  explicit scope confirmation. Those UI panels show unsupported states, not
  mock data. Printed lists/profiles use the real authorized data.

## Enrollment history

- A student can have multiple enrollment rows over time. Do not overwrite or delete prior rows when promoting, transferring, or leaving a class.
- Attendance and daily reports reference the enrollment row, not only the student or current class.
- Transfers are one transaction: close the old enrollment, create the new enrollment, record the actor/time, and preserve the previous row.
- **Approved Student delivery:** implement `StudentInClass` using half-open
  `[StartDate, EndDate)` periods, with at most one class at a time across all
  history. PostgreSQL's approved bundled `btree_gist` extension and exclusion
  constraint reject overlaps, including concurrent requests.
- Initial enrollment may be backdated, but not before the child's birthday or
  outside the selected class's live same-school year, and not into the future.
  EndDate initially equals the day after the year ends; enrollment naturally
  expires at that boundary. School-local dates use `Asia/Ho_Chi_Minh`.
- Transfers, withdrawal and re-enrollment take effect today. A transfer/withdrawal
  cannot end an enrollment on its start date. Closing a period may only shorten
  its EndDate to today; closed history, class/student identity and start dates
  cannot be rewritten or deleted. Gaps and profiles without enrollment are allowed.
- Status is derived: `Đang theo học` for an effective enrollment, `Chưa phân lớp`
  if never enrolled, otherwise `Đã nghỉ học`. `Mới nhập học` means the first
  enrollment starts in the current school-local month, not the latest transfer.
  UI age-group and study-mode choices are stored on each enrollment; they do not
  introduce class capacity or age-admission rules.
- Admin manages same-school profiles/enrollments. Teacher reads only students
  currently enrolled in their active assigned classes and sees only those current
  enrollment rows, not other-class history. Parent reads only their server-mapped
  student's profile/history. Ending assignments, pausing teachers, or removing
  mappings/membership revokes the applicable access on the next request.
- Student stores the UI's personal, guardian/contact, optional measurements and
  care fields. Profile creation/editing does not create or link accounts.
  Health/contact values are never copied to audit metadata or error messages.
  Attendance, tuition and document-storage tabs show explicit unsupported states;
  no mock records replace missing APIs in real routes.

## Attendance

- MVP statuses: `Present`, `AbsentExcused`, `AbsentUnexcused`.
- At most one attendance record per enrollment per school-local date.
- Batch entry covers the selected class/date and only current authorized students. **Recommended:** all-or-nothing transaction for the initial batch operation; return validation errors that identify rows without persisting a partial batch. Confirm before implementation.
- Validate the date against the enrollment period. When both check-in and check-out exist, checkout must not precede check-in.
- Only an assigned teacher or an authorized school administrator can create/update attendance. Past records and published/reporting effects require audit details.
- **Approval required:** whether attendance can be edited after a deadline, which role may correct it, and how to represent late arrival/early pickup without expanding MVP status unnecessarily.

## Daily reports

- At most one summarized report per student per date per enrollment.
- Teachers can save drafts and publish completed reports. Parents can read only published reports for their mapped student.
- A published report is not silently removed or changed. Corrections require an authorized use case and an audit record of actor, time, and changed-field names; avoid copying health/report text into the audit log.
- Nap duration must be non-negative. `HealthNote` is sensitive; minimize collection and access.
- **Approval required:** report status values, correction/republication workflow, and retention.

## Announcements

- A published announcement has exactly one target type: school, one or more classes, or one student.
- School-wide target rows are unnecessary. Class target rows allow multiple classes. Student target rows identify exactly one student.
- At publication, validate the publisher's rights and verify every target/student belongs to the announcement's school. Resolve the eligible student set once and atomically store `AnnouncementRecipient` rows, deduplicating students.
- **Recommended:** the eligible snapshot is evaluated against enrollments effective at one publication instant/date, stored with `PublishedAt`, and written in the same transaction. The exact school-local boundary and treatment of withdrawn students require approval.
- Recipients remain fixed when students later move classes. `ReadAt` is the only parent-facing recipient update and is set only by the mapped student account.
- Copying creates a new draft with copied title/content and `CopyOfAnnouncementId`; it does not copy targets, recipients, read states, or publication timestamps. Attachments are excluded until an attachment module is designed.

## Audit and privacy

- **Required and implemented 2026-11-03:** existing School, SchoolYear,
  Classroom, Student, SchoolMembership and StudentAccount records carry UTC
  creation/latest-action metadata and use soft deletion. D records cannot be
  updated or restored; creation and identity metadata are immutable.
- Deletion does not cascade to historical/dependent records. Membership and
  student-account identity uniqueness remains reserved after soft deletion;
  school-year and classroom names use active-record uniqueness.
- Actor identity comes from authentication, never request payloads. Named
  background/system operations use null user IDs with a historical event naming
  the operation. Unknown legacy creators remain unknown.
- Latest-row metadata is not full history. Append-only AuditLogs retain events
  separately; SchoolAdmin archive/audit reads remain scoped to the resolved school.
  See [audit and soft-delete policy](./audit-and-soft-delete.md) and
  [migration/backfill notes](./audit-migration-notes.md).

- Audit high-impact student changes, enrollment transfers, attendance changes, report publication/corrections, announcement publication, and account/permission changes.
- Keep audit data minimal: actor, school, action, entity reference, timestamp, and allowlisted changed-field metadata. Never log passwords, tokens, full child health notes, or private contact values.
- Use fake student data in development, tests, prompts, and logs. See [security and privacy](./security.md).
