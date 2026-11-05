# Figma screen mapping

Audit date: 2026-11-02.

Source: [current Admin prototype](https://www.figma.com/design/a55x7j0hZekxwTBhysSLuW/Preschool-Connect---Admin-Prototype?node-id=0-1),
page `0:1` ("Preschool Connect Admin"). The user supplied this replacement file
on 2026-11-02. Its live browser layer tree returned 23 screen frames and one UI
reference frame. Node IDs below were read from the new file's layer elements,
not copied from the previous file.

**MCP blocked:** Figma MCP rejected the initial `get_design_context` requests with
"You've reached the Figma MCP tool call limit on the Starter plan." No MCP design
context, assets, or Code Connect mappings have been retrieved.

The user has approved current exported screenshots, tokens/assets, and interaction
specifications as an alternative source. Browser access to the replacement file
is now authenticated. The user supplied 22 PNG exports: the UI reference and
21 application screens, including the latest 19-screen batch. Help is the only
mapped application screen without an export. The inventory below enumerates 22
application screens plus the UI reference; the earlier "23 screens plus reference"
count was incorrect. Original logo/icons/font files are still missing.

All supplied exports have explicit `/preview` routes. Their `/admin` equivalents
remain API-gated; auth, class, school-year and Teacher operations below
are wired to persistence. The preview is not a backend implementation or policy approval.

## Existing application and API baseline

- `client/src/App.tsx` now composes pathname-based navigation, auth, and shared
  shell. Feature pages and API calls are separated.
- Preserved UI: readiness, login, signed-in identity, tenant context, school-year
  create/list, and class create/list; added session restore and logout.
- Existing dependencies: React and React DOM; no router, form library, query
  library is installed. Shared UI components extend the existing CSS approach.
- Existing API definitions are in `server/PreschoolConnect.Api/Program.cs`.
- Session: `GET /api/auth/csrf`, `GET /api/auth/me`,
  `POST /api/auth/login`, `POST /api/auth/logout`.
- School context: `GET /api/tenant/context`.
- School setup: `GET/POST /api/school-years`, `GET/POST /api/classes`.
- School-year extension: `PUT/DELETE /api/school-years/{id}`; GET now includes
  same-school class counts. Class/year mutations also require completed migrations.
- Teacher extension: real `/admin/teachers`, `/admin/teachers/new`,
  `/admin/teachers/{id}` and `/admin/teachers/{id}/edit` retain the existing
  list/form/profile layout. Search, class/status tabs/filters, pagination,
  statistics and print use authorized real data. `GET/POST /api/teachers`,
  `GET/PUT /api/teachers/{id}`, eligible `/accounts`, assignment POST and
  assignment DELETE support personnel editing and immediate class assignment.
  Admin sees school profiles/history; Teacher sees only their linked profile
  and current authorized classes; Parent is denied. Account creation, timetable
  and file upload are excluded by confirmed scope; the latter panels display
  unsupported states without sample data. Preview routes remain synthetic-only.
- Operations: `GET /api/health`, `GET /api/health/ready`.
- Mutations must send JWT Bearer, antiforgery cookie/CSRF, server-validated school
  context, and SchoolAdmin authorization for school-year/class creation.

### Classroom API contract - 2026-11-04

Approved scope is the existing real list and name/year creation form, not the
richer preview-only class form.

| UI requirement | API capability |
|---|---|
| Name/year list and basic information dialog | `GET /api/classes` returns `[{ id, schoolId, schoolYearId, name }]`, active classes in the server-resolved school ordered by name. The dialog uses the selected list row; no separate detail API is needed. |
| Name search and SchoolYear filter | Applied locally to the authorized list; no duplicate search endpoint. Options come from `GET /api/school-years`. |
| Add class | Existing `POST /api/classes` accepts only `{ name, schoolYearId }`; returns 201 with `{ id, schoolId, schoolYearId, name }`. The client reloads the authorized list after saving. |
| Empty/permission/error states | Empty lists remain empty; no preview fallback. The add button requires SchoolAdmin; server enforces the same rule and antiforgery protection. |

Names are trimmed, required, at most 100 characters, and case-insensitively
unique within a year among non-deleted classes (including Vietnamese letters).
400 covers validation, invalid CSRF and unmapped ownership/audit properties;
401 requires authentication; 403 denies membership/role access; 404 rejects
missing/deleted/cross-school years; 409 covers duplicate names, including
concurrent inserts. Pending migrations return 503 through tenant resolution.
The request never accepts SchoolId or audit fields. SchoolYearId must reference
an active same-school year and cannot later be reassigned.

Creation uses the existing transactional audit pipeline. Retained class
tombstones remain related to their original year and still prevent year
deletion/date changes. No class update/delete API, roster, assignment, capacity,
room, age-group or start-date backend was added. Those remain explicitly
separate preview fixtures, never data in the real Classroom workflow.

## Frame inventory

Names below retain the live metadata labels. Detailed verification and visual
limitations are in the implementation-status document.

| Figma frame | Node ID | Real workspace route | Preview implementation | Related API / dependency | Shared components |
|---|---|---|---|---|---|
| Preschool Connect · Tổng quan | `15:45117` | `/admin/dashboard` | Implemented first slice; full synthetic visual preview at `/preview/dashboard`, actual workspace shows only available class count | Classes/years/context available; dashboard statistics missing | Shell, PageHeader, Metric, EmptyState |
| Preschool Connect · Học sinh | `15:44874` | `/admin/students` | `/preview/students`: query, class/status tabs/filters, local pagination | Student list API missing | DataTable, Search, Tabs, Pagination |
| Preschool Connect · Hồ sơ học sinh | `15:44662` | `/admin/students/:studentId` | `/preview/students/HS0261`: profile, attendance/fees/documents tabs, edit link | Student detail/update API missing | Facts, Panel, Tabs, FilePicker |
| Preschool Connect · Thêm học sinh | `15:44431` | `/admin/students/new` | `/preview/students/new`: validated form and attachment selection, no upload/save | Student create API missing | PreviewForm, Field, FilePicker |
| Preschool Connect · Lớp học | `15:44130` | `/admin/classes` | Redesigned real list/search/year filter; grouped synthetic preview at `/preview/classes` | `GET /api/classes`; richer class data missing | Shell, PageHeader, Metric, class tiles, EmptyState |
| Preschool Connect · Chi tiết lớp | `15:43873` | `/admin/classes/:classId` | `/preview/classes/1-1`: searchable sample roster, teacher links, routine, tabs | List data available; detail, roster, assignments missing | Panel, DataTable, Facts, Tabs |
| Preschool Connect · Thêm lớp học | `15:43680` | `/admin/classes/new` | `/preview/classes/new`: room/capacity/assignment design. Real create retains supported name/year only | `POST /api/classes`, `GET /api/school-years`; richer fields missing | PreviewForm, Field, SchoolForm |
| Preschool Connect · Giáo viên | `15:43436` | `/admin/teachers` | `/preview/teachers`: query, class/status filters, pagination | Teacher list API missing | DataTable, Search, Tabs, Pagination |
| Preschool Connect · Hồ sơ giáo viên | `15:43226` | `/admin/teachers/:teacherId` | `/preview/teachers/GV001`: profile, schedule, documents, edit link | Teacher detail/update API missing | Facts, Panel, Tabs |
| Preschool Connect · Thêm giáo viên | `15:43015` | `/admin/teachers/new` | `/preview/teachers/new`: personal/professional/work fields; no account creation | Teacher create API missing | PreviewForm, Field |
| Preschool Connect · Điểm danh | `15:42741` | `/admin/attendance` | `/preview/attendance`: date/class/session, radios, mark-all, notes isolated in memory | Attendance read/write API missing | DataTable, Stats, Pagination |
| Preschool Connect · Lịch & sự kiện | `15:42481` | `/admin/events` | `/preview/events`: Monday-first calendar, month navigation, week/list samples | Events API missing | Tabs, Panel, RouteLink |
| Preschool Connect · Chi tiết sự kiện | `15:42279` | `/admin/events/:eventId` | `/preview/events/nature`: programme/coordinator/participants; other fixtures summary only | Event detail API missing | Facts, DataTable, Tabs |
| Preschool Connect · Tạo sự kiện | `15:42071` | `/admin/events/new` | `/preview/events/new`: date/time validation, audience/coordinator fields | Event create API missing | PreviewForm, Field |
| Preschool Connect · Thông báo | `15:41823` | `/admin/announcements` | `/preview/announcements`: status/audience/month/query filters, pagination | Announcements API missing | DataTable, Tabs, Search |
| Preschool Connect · Soạn thông báo | `15:41630` | `/admin/announcements/new` | `/preview/announcements/new`: live content preview, required scheduled date/time, local files | Draft/publish/recipient APIs missing | PreviewForm, FilePicker, Facts |
| Preschool Connect · Học phí | `15:41377` | `/admin/fees` | `/preview/fees`: period/class/status/query filters, pagination | No fee API; tuition/payment excluded from MVP | DataTable, Tabs, Stats |
| Preschool Connect · Chi tiết học phí | `15:41155` | `/admin/fees/:feeId` | `/preview/fees/HS0261`: breakdown/history and prospective balance calculation only | No fee detail/payment API; UI-only scope | Facts, DataTable, Panel |
| Preschool Connect · Báo cáo | `15:40872` | `/admin/reports` | `/preview/reports`: sample snapshot, group/date/period filters, chart/table tabs | Reporting APIs missing | Stats, Tabs, DataTable |
| Preschool Connect · Cài đặt | `15:40628` | `/admin/settings` | `/preview/settings`: school/year/contact/display form and switches. Real route retains year create/list | Years/context available; other settings APIs missing | PreviewForm, Field, SchoolForm |
| Preschool Connect · Phân quyền & tài khoản | `15:40351` | `/admin/permissions` | `/preview/permissions`: prototype matrix, account filters, invitation validation and local hide | Account/role administration and email APIs missing; new roles not approved | DataTable, Pagination, Field |
| Preschool Connect · Trung tâm hỗ trợ | `15:40126` | `/admin/help` | None | No support submission API | Shell, search, help sections |
| Preschool Connect · Mẫu hệ thống giao diện | `15:39832` | No application route; design reference | Export inspected; semantic CSS tokens, fields, metrics, badges, cards and focus states added | No API required | PageHeader, Metric, EmptyState, Message; shared CSS |

## Additional states and boundaries

- Year-management extension (2026-11-03): `/preview/settings/years` and
  `/admin/settings/years` implement the approved list/add/edit/delete workflow;
  the real settings entry also opens it. This reuses settings visual conventions,
  has no separate Figma frame ID, and introduces no persisted status/default year.
  Used-year dates/deletion are locked, with matching server/database checks.
- `/login`: JWT login in separate layout, with memory-only 15-minute sessions; no dedicated
  top-level login frame was returned by the inspected page metadata.
- Preview profiles/classes/events/announcements support `/:id/edit` using their
  supplied create-form layouts. These are adaptations, not separate Figma frames.
  Invalid IDs and unsupported path shapes show not-found states.
- Preserve school-year create/list functionality even though no separate
  top-level school-year frame was returned.
- The new dashboard inspector shows a 1440 x 1129 frame. Other new frame
  dimensions and responsive references remain to be inspected.
- API gaps must remain explicit. Do not represent synthetic UI data as persisted
  school data or report successful save/publish/delete/payment without an API.
- Figma includes fees, but the [product scope](../product-scope.md) excludes
  tuition/payment from MVP. The redesign request permits UI-only coverage;
  it does not approve backend fee/payment implementation.
- Any synthetic examples must use Vietnamese sample data, never real children
  or parent records.
- `/preview/*` is a visibly labelled, non-persisting visual fixture area, not an
  alternate way to access authenticated data. Preview initialization makes no
  API requests. Unsupported real screens show API gaps and links into the
  explicitly separate preview. Help remains a missing-design notice.

See [implementation status](./figma-implementation-status.md) for phase tracking.
