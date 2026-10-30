# PreschoolConnect

PreschoolConnect is a local-development school operations app. The API and web client run as separate processes and use the project-local PostgreSQL instance.

## Local development

Prerequisites: the project-pinned .NET 10 SDK, Node.js/npm, and the PostgreSQL 17.7 local distribution under `.postgres`.

1. Start PostgreSQL:

   ```powershell
   .\scripts\start-local-postgres.ps1
   ```

2. In a separate PowerShell window, start the API. Set a private development-only password to opt into creating the synthetic admin account (`admin@northside.test`); there is no default password:

   ```powershell
   $env:DOTNET_ROOT = "$HOME\.dotnet"
   $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   $env:ASPNETCORE_URLS = "http://127.0.0.1:5155"
   $env:Seed__AdminPassword = Read-Host "Development admin password"
   dotnet run --no-launch-profile --project .\server\PreschoolConnect.Api\PreschoolConnect.Api.csproj
   ```

   The password must satisfy ASP.NET Identity's development password policy. The API applies pending EF Core migrations at startup. Keep this development service bound to loopback and use synthetic data only.

3. In another PowerShell window, start the web client:

   ```powershell
   Set-Location .\client
   npm ci
   npm run dev -- --host 127.0.0.1
   ```

4. Open `http://127.0.0.1:5173`. The Vite development server proxies `/api` requests to `http://localhost:5155`.

The client shows API/database status and provides sign-in plus initial school-year/class management for an authorized school administrator. Read [implementation status](./docs/implementation-status.md) for limitations; this is not a production-ready or pilot-ready system.

## Verification

From the project root:

```powershell
.\scripts\verify.ps1
```
