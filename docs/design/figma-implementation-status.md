# Figma redesign implementation status

Updated: 2026-11-02. **All supplied exports implemented as previews; production workflows incomplete.**

## School-year extension - 2026-11-03

The requested year-management extension is an adaptation of the existing
settings palette/components, not a new Figma frame or pixel-matched export.
`/preview/settings/years` now has a year list, search/date-phase filter, metrics,
rules sidebar, create/edit dialogs and delete confirmation. Sample mutations
are explicitly in-memory, not persisted; reload resets them. The old embedded
year/status fields in the general settings preview are replaced by a link here.

`/admin/settings/years` and the real settings entry use the same manager with
GET/POST/PUT/DELETE integrations. SchoolAdmin controls are gated; Teacher remains
read-only. Server and PostgreSQL enforce the user-approved rules: unique trimmed
case-insensitive names, non-overlapping inclusive dates, used-year date lock and
restricted deletion. No new active/archive flag or automatic/default year.

Verification for this extension: 22 backend tests including 3 real PostgreSQL
integrity/migration/concurrency tests on disposable synthetic databases, 8
frontend tests, strict build, browser preview CRUD and API-stub real CRUD,
conflicts, header/payload checks, read-only role and mobile/desktop overflow.
Native modal close/reopen was checked repeatedly. Integrated-browser Escape
automation did not reliably deliver native cancel; the Escape handler was
verified with dispatched key events. This is not physical keyboard/device QA.
The user preview server remains available separately from the disposable test
database. No ordinary local database migration or deployment was performed.

## Source and approval

- Current source: [Admin prototype](https://www.figma.com/design/a55x7j0hZekxwTBhysSLuW/Preschool-Connect---Admin-Prototype?node-id=0-1).
- The mapping enumerates 22 application screens plus a UI reference (correcting
  the earlier count). See the
  [screen mapping](./figma-screen-mapping.md) for all node IDs/API dependencies.
- At the time this status was recorded, MCP authentication was reported to work,
  but Starter quota blocked design-context/screenshots. The separate
  [integration setup record](../integrations/figma-mcp.md) reports OAuth was
  incomplete. Current VS Code MCP connectivity was not independently verified
  during the 2026-11-03 remediation. Browser access to the design file, VS Code
  MCP OAuth, ChatGPT MCP, and Figma AI/Agent credits are distinct states.
  No quota-consuming request was made during the remediation.
- User approved current exports as an alternative and supplied PNGs for the
  UI reference `15:39832`, dashboard `15:45117`, classes `15:44130`, and the
  remaining 19 management/operations/administration screens. Only Help is absent.
- Those PNGs were inspected as visual references, not embedded as page images.
  No attachments were edited. Original logos/icons/font files were not provided.

## Implemented

### Foundation and shell

- Semantic CSS palette, 18 px card/10 px control radii, 248 px desktop sidebar,
  84 px topbar, typography scale, shared fields, badges, progress bars and cards.
- Shared `PageHeader`, `Metric`, `EmptyState`, `Message`, `RouteLink`, `Panel`,
  `Facts`, `Stats`, `Tabs`, typed `DataTable`, search/pagination/filter helpers,
  form/field primitives and local-only attachment selection.
- Sidebar, breadcrumb, account menu, logout, mobile navigation, visible focus,
  skip link, native modal with Escape/focus containment, labelled forms/search.
- Small pathname/history navigation, without adopting a router dependency.
  Hosting must fall back to `index.html` for SPA page URLs.
- Cookie credentials, per-mutation CSRF retrieval and school headers preserved.
  Added existing `/api/auth/me` session restoration and `/api/auth/logout`.
  Backend remains the authorization boundary.

### Screens and data

| Route | Delivered behavior |
|---|---|
| `/login` | JWT login, memory-only 15-minute session, loading/errors, readiness, link to explicit preview |
| `/admin/dashboard` | Shared visual structure and real class count; unavailable statistics show no-data state |
| `/preview/dashboard` | Synthetic chart, cards, events, activities and group summary from supplied screenshot |
| `/admin/classes` | Real list, class-name search, year filter, empty state, basic available-field information dialog |
| `/preview/classes` | Four age groups, 12 synthetic classes, capacity bars, search/empty state |
| `/preview/classes/1-1`, `/preview/classes/new` | Class detail, sample roster/teacher links/routine, room/capacity/assignment form |
| `/preview/students`, `/preview/teachers` | Search, class/status filters, tabs, pagination, profile navigation |
| `/preview/students/HS0261`, `/preview/teachers/GV001` | Profile sections, attendance/fees/schedule/documents tabs, edit navigation |
| `/preview/students/new`, `/preview/teachers/new` | Required fields, email/phone/date validation, local file selection |
| `/preview/attendance` | Per-date/session/class in-memory statuses and notes, mark-all-present, counts |
| `/preview/events`, `/preview/events/nature`, `/preview/events/new` | Month calendar/navigation, week/list samples, detail/programme, validated date/time form |
| `/preview/announcements`, `/preview/announcements/new` | Query/status/audience/period filters; live compose preview, conditional scheduling validation, local files |
| `/preview/fees`, `/preview/fees/HS0261` | Filters, breakdown, history, prospective payment/balance calculation with bounds; no transaction |
| `/preview/reports` | Snapshot/group/date/period filters, chart/distribution/table tabs; missing-data state |
| `/preview/settings`, `/preview/settings/years` | School/contact/display preview; separate interactive year manager added 2026-11-03 |
| `/preview/permissions` | Prototype role matrix, account filters, invite validation, local invitation hide; no email or role mutation |
| `/admin/classes/new` | Existing real create API, name/year only, validation, pending state, success only after successful POST |
| `/admin/settings`, `/admin/settings/years` | Real year CRUD manager added 2026-11-03; read-only for non-admin members |

Preview routes have an explicit sample-data notice and do not call APIs or
persist anything. They are separate from the real school workspace. Unknown
student counts, staff assignments and capacity are not fabricated on real pages.
Other `/admin` features show their missing APIs and links to the separate
preview, never sample records in the real workspace. Profile/class/event/
announcement `/:id/edit` previews reuse supplied create layouts, without
claiming separate Figma frames or persisted updates. Unknown IDs and unsupported
deep paths do not silently become new forms or unrelated lists.

Lists contain only 8 student and 9 teacher fixtures; their pagination reflects
those counts, not the static school-wide design totals. Extra fixture profile
fields are illustrative rather than independent exported designs. Student fee
links and figures share the same fixtures as the fee pages.

## Phase tracking

| Phase | Status |
|---|---|
| A - Audit | Existing client/API audited; all supplied exports inspected; Help source missing |
| B - Foundation | Shared components implemented; original assets/font missing |
| C - Shell/routing | Preview and real boundaries, auth/session/logout, deep links, not-found states implemented |
| D - Dashboard | Synthetic visual preview and honest real-data shell implemented; analytics APIs missing |
| E - Management | Supplied class/student/teacher lists/details/forms implemented as previews; real class/year behavior preserved |
| F - Attendance/events | Preview implementation and interactions checked; backend missing |
| G - Announcements/fees | Preview implementation checked; no publish, receipt or payment; finance remains outside MVP |
| H - Reports/settings/permissions/help | Supplied screens previewed; school-year setup preserved; Help blocked by missing export |
| I - QA | Build/tests and desktop/tablet/mobile smoke checks passed; not full accessibility, visual-regression or live-persistence QA |

## Verification

- `client`: `npm run build` passed, now includes `tsc --noEmit` before Vite.
- `npm test`: **4/4 test groups passed**, covering mapped route shapes, invalid
  deep paths, event date/time ordering and conditional schedule requirements.
  Uses Node's built-in runner/type stripping, no added packages.
- `scripts/verify.ps1` now includes the frontend tests before the build.
- Established `scripts/verify.ps1` could not start because it assumes
  `$HOME\.dotnet\dotnet.exe`, absent on this machine. No script/system changes.
- Available system SDK includes the pinned .NET 10.0.403. Equivalent backend
  command `dotnet test ... --no-restore --nologo -v minimal` passed **13/13**.
- Temporary Vite server bound to `127.0.0.1:5187`; HTTP readiness returned 200.
- Desktop browser 1440 px: class preview rendered 12 cards across 2/3/3/4 groups;
  search reduced to one match; unmatched search showed empty state; no horizontal
  page overflow. Dashboard/classes rendered screenshots compared with user PNGs.
- Mobile 390 px: search reduced to one class, menu opened/closed on navigation,
  history Back returned to classes, dashboard had no horizontal page overflow.
  Dashboard overflow checks also passed at 320, 768, 1024 and 1440 px; mobile
  screenshot inspected at 390 px.
  Browser click automation had actionability timeouts; DOM click activation was
  used for these interaction checks. This is not a physical touch-device test.
- Browser API-stub checks (synthetic only): session restoration, real-data class
  list, class dialog/Escape, class create, year create/list, login and logout.
  Checked mutation bodies plus `X-CSRF-TOKEN` and `X-School-Id` headers.
- Stubbed class POST 403 showed an explicit error and no success notification.
- Stubbed Teacher membership: create-class action disabled; school-year list
  still visible, with creation disabled.
- These stub checks do not prove live authenticated persistence. No real student
  data or database was used for browser testing.
- Latest-batch browser smoke: 20 route loads (19 new screens plus year settings)
  at 1440 px, no runtime errors and no API requests. Another 38 checks across
  19 new screens at 390 and 768 px found no horizontal page overflow. Tables and
  calendar use focusable local scroll regions where needed.
- Latest interaction checks passed: student query/pagination/profile/edit/fee
  identity, active-teacher filter, event reversed dates/times, live compose title,
  required schedule date/time, payment maximum and exact remaining balance,
  attendance session isolation/retention including notes, mark-all, calendar
  month/week/list, matching teacher links, settings tabs/form, account role
  filter/invite/local hide, report group/missing-snapshot states, invalid IDs/
  deep routes and mobile menu open/close.
- Rechecked 21 preview screen loads plus a form submission with browser fetch
  instrumentation: **zero API calls**. Rechecked actual workspace against
  synthetic browser API stubs: session restore, class/year create and refresh,
  mutation credentials/CSRF/school header and payload, 403 without save success,
  logout/login, Teacher cannot create classes/years but can list years.
- Desktop screenshots inspected for student list and permissions against their
  exports. This is representative visual review, not pixel-diff coverage of
  every supplied frame.
- No ESLint configured; none added. No new dependencies,
  backend changes, commits, pushes or deployment.

## Known visual and functional differences

- Not pixel-perfect. Inter is declared but falls back to installed Segoe UI
  without a supplied font file; typography geometry may differ.
- Original logo/icons unavailable. Wordmark is plain text; metric/activity marks
  are decorative color blocks, not claimed to be matching Figma icon assets.
- Sample-data notices, unavailable labels, disabled actions and missing-data
  states intentionally differ from the static prototype for honesty.
- Class sample names/teacher labels are synthetic. Real API only has class name,
  year and identifiers; no age grouping, occupancy, staff or lifecycle status.
- Dashboard search/notification UI not implemented: no corresponding global
  search/notification API. Class search is functional on the list and mobile.
- Screenshot chart is a static synthetic preview, without date-range interaction.
- Real login, available-field class-create, year settings and class-information
  dialog retain their integrations in shared styling. Richer screenshot fields
  exist only in the preview because their APIs do not exist.
- The calendar's week tab is a sample of 02–08/11, not a full navigable weekly
  scheduler. Reports are a fixed sample snapshot, not an analytics engine.
  School-wide design totals are explicitly static and not recalculated by all
  filters. Enrollment trends are absent, not inferred.
- Only the nature event has an exported detailed programme. Other event details
  show summaries and a missing-detail notice, not a copied nature programme.
  No announcement detail content was supplied; its absence is explicit.
- Prototype Kế toán/Ban giám hiệu roles, invitation expiry, two-teacher/opening
  conditions and display preferences do not become backend business rules.
- Mobile/tablet behavior is a responsive adaptation; no mobile reference supplied.
- Full accessibility audit/contrast measurements and live integration QA pending.

## Remaining work

Provide the Help export plus original SVG/PNG logo/icons and approved font assets
for closer visual fidelity. Approve and implement business APIs as separate
server-authorized, tested vertical slices before connecting preview workflows
to real records. Complete live persistence, accessibility/contrast and visual
regression QA before pilot use. No commit, push or deployment performed.
