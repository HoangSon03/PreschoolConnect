# Graph Report - PreschoolConnect  (2026-11-01)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 351 nodes · 597 edges · 28 communities (21 shown, 7 thin omitted)
- Extraction: 98% EXTRACTED · 2% INFERRED · 0% AMBIGUOUS · INFERRED: 10 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `e1bf9edc`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Community 0
- Community 1
- Community 2
- Community 3
- Community 4
- Community 5
- Community 6
- Community 7
- Community 8
- Community 9
- Community 10
- Community 11
- Community 12
- Community 13
- Community 14
- Community 15
- Community 16
- Community 17
- Community 18
- Community 19
- Community 20
- Community 21
- Community 22
- Community 23

## God Nodes (most connected - your core abstractions)
1. `ApplicationDbContext` - 26 edges
2. `compilerOptions` - 17 edges
3. `HealthEndpointTests` - 16 edges
4. `ApplicationUser` - 16 edges
5. `SchoolMembership` - 15 edges
6. `Student` - 14 edges
7. `CustomWebApplicationFactory` - 13 edges
8. `PreschoolConnect.Application.Models` - 13 edges
9. `TenantContextTests` - 12 edges
10. `School` - 12 edges

## Surprising Connections (you probably didn't know these)
- `CustomWebApplicationFactory` --references--> `Program`  [EXTRACTED]
  server/PreschoolConnect.Tests/CustomWebApplicationFactory.cs → server/PreschoolConnect.Api/Program.cs
- `SchoolMembership` --references--> `SchoolMembershipRole`  [EXTRACTED]
  server/PreschoolConnect.Application/Models/SchoolMembership.cs → server/PreschoolConnect.Application/Models/SchoolMembershipRole.cs
- `ApplicationDbContext` --references--> `SchoolMembership`  [EXTRACTED]
  server/PreschoolConnect.Infrastructure/Persistence/ApplicationDbContext.cs → server/PreschoolConnect.Application/Models/SchoolMembership.cs
- `ApplicationDbContext` --references--> `ApplicationUser`  [EXTRACTED]
  server/PreschoolConnect.Infrastructure/Persistence/ApplicationDbContext.cs → server/PreschoolConnect.Application/Identity/ApplicationUser.cs
- `ApplicationDbContext` --references--> `School`  [EXTRACTED]
  server/PreschoolConnect.Infrastructure/Persistence/ApplicationDbContext.cs → server/PreschoolConnect.Application/Models/School.cs

## Import Cycles
- None detected.

## Communities (28 total, 7 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.09
Nodes (15): CustomWebApplicationFactory, UseInMemoryDatabase, CsrfResponse, requestToken, HealthEndpointTests, HealthResponse, status, LoginResponse (+7 more)

### Community 1 - "Community 1"
Cohesion: 0.07
Nodes (31): dependencies, react@^19.1.0, react-dom@^19.1.0, devDependencies, @types/react@^19.1.3, @types/react-dom@^19.1.3, typescript@^5.8.3, vite@^7.1.0 (+23 more)

### Community 2 - "Community 2"
Cohesion: 0.10
Nodes (19): Student, CreatedUtc, DateOfBirth, FirstName, Id, IsActive, LastName, PreferredName (+11 more)

### Community 3 - "Community 3"
Cohesion: 0.15
Nodes (4): PreschoolConnect.Infrastructure.Persistence, PreschoolConnect.Application.Identity, PreschoolConnect.Tests, PreschoolConnect.Infrastructure.Persistence.Migrations

### Community 4 - "Community 4"
Cohesion: 0.10
Nodes (19): compilerOptions, allowJs, allowSyntheticDefaultImports, esModuleInterop, forceConsistentCasingInFileNames, isolatedModules, jsx, lib (+11 more)

### Community 5 - "Community 5"
Cohesion: 0.10
Nodes (13): PreschoolConnect.Application.Auth, ClassroomRequest, Program, SchoolYearRequest, AuthenticatedUserResponse, Email, FullName, IsActive (+5 more)

### Community 6 - "Community 6"
Cohesion: 0.10
Nodes (19): POSTGRES_DB, POSTGRES_PASSWORD, POSTGRES_USER, interval, retries, test, timeout, container_name (+11 more)

### Community 7 - "Community 7"
Cohesion: 0.14
Nodes (15): Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.14), Microsoft.AspNetCore.Mvc.Testing (10.0.14), Microsoft.AspNetCore.OpenApi (10.0.14), Microsoft.EntityFrameworkCore (10.0.14), Microsoft.EntityFrameworkCore.InMemory (10.0.14), Microsoft.NET.Test.Sdk (17.12.0), xunit (2.9.2), xunit.runner.visualstudio (3.0.0) (+7 more)

### Community 8 - "Community 8"
Cohesion: 0.23
Nodes (8): SchoolContext, SchoolContextFailure, MembershipNotFound, NoActiveMembership, None, SelectionRequired, SchoolContextResolution, SchoolContextResolver

### Community 9 - "Community 9"
Cohesion: 0.20
Nodes (8): ApplicationDbContext, Classrooms, SchoolMembershipRoles, SchoolMemberships, Schools, SchoolYears, StudentAccounts, Students

### Community 10 - "Community 10"
Cohesion: 0.20
Nodes (9): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, profiles, PreschoolConnect.Api (+1 more)

### Community 11 - "Community 11"
Cohesion: 0.25
Nodes (7): compilerOptions, allowSyntheticDefaultImports, composite, module, moduleResolution, skipLibCheck, include

### Community 12 - "Community 12"
Cohesion: 0.25
Nodes (7): SchoolMembership, CreatedUtc, Id, IsActive, Roles, SchoolId, UserId

### Community 13 - "Community 13"
Cohesion: 0.25
Nodes (7): ApplicationUser, CreatedUtc, FirstName, IsActive, LastName, SchoolId, StudentId

### Community 14 - "Community 14"
Cohesion: 0.29
Nodes (6): School, CreatedUtc, Id, IsActive, Name, Slug

### Community 15 - "Community 15"
Cohesion: 0.29
Nodes (6): SchoolYear, EndDate, Id, Name, SchoolId, StartDate

### Community 17 - "Community 17"
Cohesion: 0.33
Nodes (5): Classroom, Id, Name, SchoolId, SchoolYearId

### Community 18 - "Community 18"
Cohesion: 0.33
Nodes (6): StudentAccount, CreatedUtc, Id, SchoolId, StudentId, UserId

### Community 21 - "Community 21"
Cohesion: 0.50
Nodes (3): SchoolMembershipRole, MembershipId, Role

### Community 22 - "Community 22"
Cohesion: 0.50
Nodes (4): TenantContextResponse, membershipId, roles, schoolId

### Community 23 - "Community 23"
Cohesion: 0.67
Nodes (3): ReadinessResponse, database, status

## Knowledge Gaps
- **88 isolated node(s):** `UseInMemoryDatabase`, `requestToken`, `status`, `message`, `requestToken` (+83 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 175 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **7 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `ApplicationDbContext` connect `Community 9` to `Community 0`, `Community 2`, `Community 3`, `Community 8`, `Community 12`, `Community 13`, `Community 14`, `Community 15`, `Community 17`, `Community 18`, `Community 19`, `Community 21`?**
  _High betweenness centrality (0.072) - this node is a cross-community bridge._
- **What connects `UseInMemoryDatabase`, `requestToken`, `status` to the rest of the system?**
  _88 weakly-connected nodes found - possible missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.08944793850454227 - nodes in this community are weakly interconnected._
- **Why does `CustomWebApplicationFactory` connect `Community 0` to `Community 3`, `Community 5`?**
  _High betweenness centrality (0.052) - this node is a cross-community bridge._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.0746031746031746 - nodes in this community are weakly interconnected._
- **Why does `HealthEndpointTests` connect `Community 0` to `Community 3`, `Community 23`?**
  _High betweenness centrality (0.045) - this node is a cross-community bridge._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.09523809523809523 - nodes in this community are weakly interconnected._