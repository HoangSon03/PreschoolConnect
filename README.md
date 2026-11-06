# PreschoolConnect

PreschoolConnect is a local-development school operations app. The API and web client run as separate processes and use the project-local PostgreSQL instance.

## Local development

Prerequisites: the project-pinned .NET 10 SDK, Node.js/npm, and the PostgreSQL 17.11 local distribution under `.postgres`.

1. Start PostgreSQL:

   ```powershell
   .\scripts\start-local-postgres.ps1
   ```

2. In a separate PowerShell window, start the API. Configure JWT issuer, audience and a private random signing key; no credentials or signing key are hardcoded:

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   $env:ASPNETCORE_URLS = "http://127.0.0.1:5155"
   $env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=5432;Database=preschoolconnect;Username=postgres"
   $env:Seed__Enabled = "false"
   $env:Jwt__Issuer = "PreschoolConnect.Local"
   $env:Jwt__Audience = "PreschoolConnect.Web"
   $keyBytes = New-Object byte[] 32
   $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
   $rng.GetBytes($keyBytes)
   $rng.Dispose()
   $env:Jwt__SigningKey = [Convert]::ToBase64String($keyBytes)
   [Array]::Clear($keyBytes, 0, $keyBytes.Length)

   dotnet run --no-launch-profile --project .\server\PreschoolConnect.Api\PreschoolConnect.Api.csproj -- --migrate
   dotnet run --no-launch-profile --project .\server\PreschoolConnect.Api\PreschoolConnect.Api.csproj
   ```

   Run `--migrate` only after verifying a disposable local target and approving schema changes. The command applies pending migrations and exits; ordinary startup never migrates. A separate `ConnectionStrings__MigrationConnection` may use a migration-only role. Keep services loopback-only and synthetic. The generated key lasts for this terminal; changing it invalidates all tokens. For stable development sessions use private user secrets or environment management, never commit it. Deployment instances must share a protected key of at least 32 random bytes.

   To run against an already prepared local database without startup seed writes,
   run only the ordinary API command (not `--migrate`). Development initialization
   defaults off; `Seed__Enabled=true` explicitly opts into synthetic school/role
   writes, not account creation. Keep Development for local HTTP;
   Staging/Production require HTTPS. JWT configuration is required for HTTP
   startup, but not migration, local provisioning or password-reset commands.

3. In another PowerShell window, start the web client:

   ```powershell
   Set-Location .\client
   npm ci
   npm run dev
   ```

4. Open `http://127.0.0.1:5173`. The Vite development server proxies `/api` requests to `http://localhost:5155`.

The client shows API/database status and provides sign-in plus initial school-year/class management for an authorized school administrator. Read [implementation status](./docs/implementation-status.md) for limitations; this is not a production-ready or pilot-ready system.

## Provision an initial local administrator

Only for a verified disposable loopback PostgreSQL target, with an existing
active school's UUID supplied by the operator. This command never creates a
school, changes schema, or modifies/elevates an existing user.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$env:Bootstrap__ConfirmDisposableTarget = "true"
$env:Bootstrap__SchoolId = Read-Host "Existing active school UUID"
$env:Bootstrap__AdminEmail = Read-Host "New administrator email"
$secret = Read-Host "New administrator password" -AsSecureString
$env:Bootstrap__AdminPassword = [Net.NetworkCredential]::new("", $secret).Password
try {
  dotnet run --no-launch-profile --project .\server\PreschoolConnect.Api\PreschoolConnect.Api.csproj -- --provision-local-admin
} finally {
  Remove-Item Env:\Bootstrap__AdminPassword
  Remove-Item Env:\Bootstrap__ConfirmDisposableTarget
  $secret.Dispose()
}
```

Identity hashes the password and creates a SchoolAdmin membership atomically.
The API exposes this role as Admin, never as cross-school authority.
`Seed__AdminPassword` is no longer used. Production provisioning and credential
handoff need an approved operator process; there is no public registration.

## Login session

Login returns a 15-minute JWT. React keeps it only in memory and attaches the
Bearer header; reload or expiry requires login again. There are no refresh
tokens. Logout revokes all account sessions using the Identity security stamp.
CSRF remains required. See [API contracts and security](./docs/auth-and-authorization.md).

## Reset the disposable local administrator password

Only for a verified local disposable database with synthetic data. In an
interactive PowerShell terminal at the repository root, run:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --no-launch-profile --project .\server\PreschoolConnect.Api\PreschoolConnect.Api.csproj -- --reset-local-admin-password
```

The command requires a loopback PostgreSQL connection, prompts for `RESET` and
twice for a hidden password, then resets only `admin@northside.test` through
Identity. It exits without starting HTTP, seeding or migrating. It does not create
a missing account, activate an inactive account, or grant roles. Do not put
passwords in command arguments, configuration files or chat. Identity's password
policy applies, and all existing JWTs are rejected on their next request.
Any existing temporary login lockout is not bypassed; wait for its expiry.

## Frontend design preview

With Vite running, open `/preview/dashboard`. The sidebar now links to classes,
students, teachers, attendance, events, announcements, fees, reports and settings.
Lists link to profiles/details and create/edit preview forms. Permissions are
under Settings. These explicitly labelled synthetic-data previews follow the
supplied Figma screenshots; they do not require a backend, request school data,
upload attachments, send messages, or persist edits.

The real workspace uses `/admin/dashboard`, `/admin/classes`,
`/admin/classes/new` and `/admin/settings`, with JWT Bearer authentication and
server-validated school access. Student/staff/attendance statistics are not
available yet; real pages do not substitute preview values. Login is at `/login`.
Production hosting must provide SPA fallback to `index.html` for page routes.

### School-year management

Open `/preview/settings/years` to try the new list, search/date-phase filter,
create/edit modal and delete confirmation without signing in. Sample changes
exist only in memory on this screen; reloading restores the fixtures.

The authenticated manager is at `/admin/settings/years` (also the default
`/admin/settings` page). SchoolAdmin can create/edit/delete; other authorized
school members can view. Years with classes can only be renamed, never have
their dates changed or be deleted. Names are school-unique ignoring case and
surrounding spaces among non-deleted years; their date ranges cannot overlap,
even on a shared boundary day. Delete now retains an audited tombstone, not a
physical removal; classes and historical references are never cascaded.

The API now supports `GET/POST /api/school-years` and
`PUT/DELETE /api/school-years/{id}`. GET includes `classCount`. Mutations require
JWT Bearer authentication, CSRF and server-resolved school membership.
The new PostgreSQL migration must succeed before real year/class writes are
enabled; legacy duplicate names or overlapping ranges cause it to stop without
modifying records. No automatic legacy cleanup is performed.

The existing business entities now use centralized UTC auditing and soft delete.
SchoolAdmin-only archive/audit API reads stay scoped to the resolved school.
See [audit and deletion policy](./docs/audit-and-soft-delete.md) and
[safe migration/backfill notes](./docs/audit-migration-notes.md) before restarting
the API against an existing database. Restoration and automatic purge are not
supported; Identity keeps its separate framework lifecycle.

See [Figma mapping](./docs/design/figma-screen-mapping.md) and
[redesign status/known differences](./docs/design/figma-implementation-status.md).
All supplied screen exports have preview implementations. This is not a complete
production workflow: most business APIs are absent. The Help export and original
icon/logo/font assets are still missing; visual parity is not claimed.

## Verification

From the project root:

```powershell
.\scripts\verify.ps1
```

`npm test` in `client` runs the dependency-free Node route/form regression tests
(Node 22.18+ or 24 recommended). `npm run build` includes strict TypeScript
checking and the Vite production build. The verification script runs backend
tests, frontend tests and build. It prefers `$HOME\.dotnet\dotnet.exe` when
present and otherwise uses `dotnet` from PATH; `global.json` selects the required
SDK version. Browser smoke-test coverage and limitations are in the redesign
status document.

Optional PostgreSQL integrity tests use the existing Npgsql provider and create/
drop randomly named synthetic test databases on an **isolated loopback server**.
They skip unless `PRESCHOOL_TEST_POSTGRES` is set. Never point this at production
or another project's database:

```powershell
$env:PRESCHOOL_TEST_POSTGRES = "Host=127.0.0.1;Port=55439;Database=postgres;Username=postgres"
dotnet test .\server\PreschoolConnect.Tests\PreschoolConnect.Tests.csproj --no-restore --filter "FullyQualifiedName~SchoolYearPostgresTests"
```

The example assumes that a separate disposable server is already running at
that port; the tests do not install or start PostgreSQL.
