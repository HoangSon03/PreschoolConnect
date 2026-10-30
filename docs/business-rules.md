# Business Rules

**Authority:** explicit rules in the project brief are confirmed. Rules labelled **Recommended** are defaults for review, not new confirmed requirements.

## Identity, school membership, and students

- Identity authenticates a user. An active `SchoolMembership` authorizes that user in one school; membership roles are scoped to that school.
- A user may belong to multiple schools; the server must validate the membership for each request. **Recommended:** membership may hold multiple role assignments so staff can hold more than one role in one school.
- No self-registration. Administrators provision and activate accounts. The first-login credential/setup handoff must be secure and verifiable; the delivery method is unresolved.
- One dedicated student account maps to one student, and a student has at most one dedicated student account. Student parent/guardian contact fields remain on `Student`; there is no separate `Parents` entity.
- Parent-facing APIs resolve the student from the authenticated account mapping. Never accept a client `StudentId` as proof of access.
- Student codes should be unique within a school, not assumed globally unique.

## School years, classes, and teacher assignments

- Each school year and class belongs to exactly one school. Each class belongs to exactly one school year.
- A teacher may be assigned to multiple classes. A class may have multiple assigned teachers.
- Validate all teacher/class/student relationships within the same school. Account status and active membership are checked at request time.
- **Approval required:** uniqueness and overlap rules for school-year date ranges, class names, and teacher assignment effective dates.

## Enrollment history

- A student can have multiple enrollment rows over time. Do not overwrite or delete prior rows when promoting, transferring, or leaving a class.
- Attendance and daily reports reference the enrollment row, not only the student or current class.
- Transfers are one transaction: close the old enrollment, create the new enrollment, record the actor/time, and preserve the previous row.
- **Recommended:** use half-open date intervals `[StartDate, EndDate)` so a same-day transfer has an unambiguous boundary. A null end date means no recorded end. Reject overlapping enrollment intervals for the same student, including concurrent transfer attempts.
- **Approval required:** exact status set, whether all historical periods must be non-overlapping regardless of status, same-day transfer semantics, whether a student may be temporarily unenrolled, and retroactive correction authority.

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

- Audit high-impact student changes, enrollment transfers, attendance changes, report publication/corrections, announcement publication, and account/permission changes.
- Keep audit data minimal: actor, school, action, entity reference, timestamp, and allowlisted changed-field metadata. Never log passwords, tokens, full child health notes, or private contact values.
- Use fake student data in development, tests, prompts, and logs. See [security and privacy](./security.md).
