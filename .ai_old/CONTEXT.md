# Project Context

## Project Summary

- Project name: DBS POS Admin Tool
- Purpose: Administer RMS+ point-of-sale installations on retail branch devices.
- Business domain: Retail POS operations and local device maintenance.
- Primary users: Local technicians or administrators working on one POS device.
- Current maturity: A functional WinUI desktop tool is retained while a .NET Agent and Angular replacement are in early migration.
- Evidence confidence: High for repository architecture and implemented behavior; external environment behavior remains partly unverified.

The retained application configures RMS+, controls Windows services, performs backup/restore and maintenance operations, and downloads branch database backups. A strangler migration is replacing the UI with a same-origin Angular application served by a loopback-only ASP.NET Core Agent; Sessions 00-02 of the migration are committed.

## Technology Stack

- Languages: C# 13, TypeScript 6.0, HTML, SCSS, XAML.
- Frontend: WinUI 3/Windows App SDK 1.8 (retained); Angular 22 standalone application (migration target).
- Backend: .NET 10 class libraries and ASP.NET Core 10 Minimal API Agent.
- Desktop or mobile: Windows 10/11 x64 desktop/local browser; no mobile target.
- Database: External Microsoft SQL Server databases used by RMS+; no application database.
- ORM or data access: No ORM; `Microsoft.Data.SqlClient` and SQL command execution.
- Testing: xUnit, ASP.NET Core `WebApplicationFactory`, Angular/Vitest, and a Playwright placeholder.
- Build tools: .NET SDK/MSBuild, npm 12, Angular CLI 22, `ng-openapi-gen`.
- Deployment: Retained WinUI supports unpackaged `win-x64` publish; Agent publish embeds the Angular production bundle. The planned Windows Service installer is not implemented.
- Infrastructure: Windows-only local execution and GitHub Actions on Windows runners.

## Repository Structure

| Path | Purpose |
|---|---|
| `src/PosAdminTool.Domain` | Core models, enums, and adapter interfaces. |
| `src/PosAdminTool.Application` | Backup, restore, cleanup, downloader, and use-case orchestration. |
| `src/PosAdminTool.Infrastructure` | SQL Server, Windows service, SMB, HTTP, connectivity, and configuration adapters. |
| `src/PosAdminTool.WinUI` | Retained WinUI shell, pages, view models, controls, and resources. |
| `src/PosAdminTool.Contracts` | Versioned API DTOs and shared public contract types. |
| `src/PosAdminTool.Agent` | Loopback ASP.NET Core host, authentication, API conventions, and file browsing. |
| `src/PosAdminTool.Web` | Angular workspace and generated API-client pipeline. |
| `tests` | Domain, application, infrastructure, and Agent integration tests. |
| `docs/migration` | Migration baseline, parity, risk, and session evidence. |
| `docs/adr` | Source ADRs for lasting migration decisions. |
| `.github/workflows/ci.yml` | .NET, Angular, Agent integration, and WinUI publish checks. |

## Main Modules

- Domain — RMS settings, backup jobs, results, service state, and abstraction interfaces; location `src/PosAdminTool.Domain`; has no project dependency.
- Application — in-process business workflows; location `src/PosAdminTool.Application`; depends on Domain.
- Infrastructure — host and external-system adapters; location `src/PosAdminTool.Infrastructure`; depends on Application and Domain.
- WinUI — retained operational UI and dependency-composition root; location `src/PosAdminTool.WinUI`; depends on Domain, Application, and Infrastructure.
- Contracts — API v1 request/response types designed not to expose secrets or raw host paths; location `src/PosAdminTool.Contracts`; independent class library.
- Agent — local HTTP host and current API foundation; location `src/PosAdminTool.Agent`; currently depends on Contracts.
- Web — Angular shell and generated typed client; location `src/PosAdminTool.Web`; build-time dependency on the Agent OpenAPI document.

## Architecture

- The retained application follows a layered architecture: WinUI view models call Application services/use cases through Domain interfaces, and Infrastructure supplies SQL, Windows service, SMB, HTTP, file, and configuration implementations.
- `App.xaml.cs` is the WinUI composition root. Operations run in the desktop process; view models own UI state and `LogHub` holds an in-memory activity stream.
- The migration runs beside WinUI. The Agent binds explicitly to `127.0.0.1:5001`, serves the Angular bundle in Production, and exposes versioned `/api/v1` endpoints.
- Current Agent APIs cover session discovery, antiforgery bootstrap, allowlisted file browsing, and opaque file handles. Operational backup/restore/service/configuration APIs are contracts only until later sessions.
- Angular production builds regenerate a typed client from the Agent OpenAPI document. Runtime communication is intended to remain same-origin.
- File handles are principal-bound, purpose-bound, single-use, five-minute in-memory records. No Agent job engine, SSE stream, or persistent audit implementation exists yet.

## Application Entry Points

- WinUI startup: `src/PosAdminTool.WinUI/App.xaml` and `App.xaml.cs`.
- Agent/API startup: `src/PosAdminTool.Agent/Program.cs`.
- Angular UI startup: `src/PosAdminTool.Web/src/main.ts`.
- Worker or service startup: Not implemented; the Agent is planned to become an installer-created Windows Service.
- .NET test execution: `dotnet test PosAdminTool.sln -c Release`.
- Angular test execution: `npm run test -- --watch=false` from `src/PosAdminTool.Web`.
- End-to-end execution: `npm run e2e` is configured; only a toolchain placeholder exists.

## Data and Storage

- SQL Server: The retained Infrastructure layer accesses configured RMS databases directly; the repository owns no database schema or migrations.
- Configuration: The retained WinUI app uses `%USERPROFILE%\.pos_admin_tool\config.json`. Its SQL secret is encrypted through `CryptoService`; known legacy secret limitations are tracked in current state.
- Main model areas: Application settings, downloader settings, service state, operation results, backup/restore metadata, and per-branch download jobs.
- Caching/state: No cache. WinUI activity and downloader state are in memory; Agent file handles are in memory.
- File storage: Local configuration, backup ZIP/BAK/config artifacts, local download folders, and remote SMB backup folders.
- Planned Agent storage: In-memory jobs plus append-only JSONL audit records for destructive actions; not implemented.

## Authentication and Authorization

- Retained WinUI: Requests process elevation at startup; privileged host actions execute in the elevated process.
- Agent authentication: Windows Integrated authentication via Negotiate.
- Agent authorization: A single policy checks the real Windows principal for local Administrators-group membership.
- Session behavior: Authenticated non-admin users may read session status but receive `isAuthorized: false`; protected file endpoints require the administrator policy.
- CSRF protection: Double-submit antiforgery cookie/header on state-changing endpoints.
- Browser session/token storage: No bearer token design; same-origin Windows authentication is used.

## External Integrations

| Name | Purpose | Direction / Protocol | Main location | Known status |
|---|---|---|---|---|
| SQL Server | Configuration checks, backup, restore, verification, reset | Outbound TDS/SQL | `src/PosAdminTool.Infrastructure/Windows/SqlCmdExecutor.cs` | Retained adapter implemented; live environment not verified in this task. |
| Windows Service Control Manager | Monitor and control RMS services | Local Windows APIs/`sc.exe` | `src/PosAdminTool.Infrastructure/Windows/WindowsServiceManager.cs` | Retained adapter implemented; no disposable-service fixture evidence. |
| RMS backup API | Trigger multi-branch backup jobs | Outbound HTTP | `src/PosAdminTool.Infrastructure/Http/BackupApiClient.cs` | Retained adapter implemented; external service not verified. |
| SMB/UNC server share | Discover and download backup ZIPs | Outbound SMB via `WNetAddConnection2` | `src/PosAdminTool.Infrastructure/Smb` | Retained adapter implemented; installed service-identity behavior is unverified. |
| RMS local files | Import and back up configuration | Local filesystem | Application use cases/services | Implemented in retained application. |
| Browser SPA | Local administration UI | Same-origin HTTP on loopback | Agent + Web | Foundation implemented; feature routes are not implemented. |

## Important Commands

```text
Install .NET: dotnet restore PosAdminTool.sln --locked-mode
Install Web:  npm ci  (from src/PosAdminTool.Web)
Build .NET:   dotnet build PosAdminTool.sln -c Release
Build Web:    npm run build  (from src/PosAdminTool.Web)
Run Agent:    dotnet run --project src/PosAdminTool.Agent/PosAdminTool.Agent.csproj
Run WinUI:    run_app.cmd
Test .NET:    dotnet test PosAdminTool.sln -c Release --no-build
Targeted:     dotnet test tests/PosAdminTool.Agent.IntegrationTests/PosAdminTool.Agent.IntegrationTests.csproj -c Release
Type check:   npm run build  (strict TypeScript/template compilation)
Lint:         npm run lint  (from src/PosAdminTool.Web)
```

## Testing Approach

- Four xUnit projects cover Domain rules, Application workflows, Infrastructure helpers, and Agent/API integration behavior.
- Agent tests use `WebApplicationFactory`, fake authentication, injected group checks, temporary directories, and a manual time provider.
- Current safety evidence includes loopback binding, contract shape/serialization, Problem Details, path traversal/reparse-point rejection, and file-handle lifecycle.
- Angular has two scaffold-level component tests. Playwright is configured but contains only a toolchain placeholder.
- No live SQL Server, SMB server, production Windows service, installer, real browser Negotiate round trip, or destructive operation is exercised by the current automated suite.

## Stable Constraints

- Support Windows 10/11 x64 only.
- Keep the Agent local and loopback-only; no cloud, central server, LAN binding, or public exposure.
- Preserve readable legacy backup ZIPs; import only non-secret legacy configuration.
- Keep WinUI available until explicit parity approval and dedicated cutover.
- Keep exact dependency versions and committed lockfiles.
- Keep C# 13 unless the proposed language-version decision is formally changed.
- Never expose secrets or absolute host paths in browser contracts, logs, or responses.
- Ship English first while using structurally RTL-ready CSS and extracted user-facing strings.
- Do not add SQLite, SignalR, a PWA, a service worker, or IndexedDB under the accepted migration design.

## Known Limitations

- Agent process restarts discard current in-memory file handles; the accepted future job design is also intentionally non-durable.
- The local Agent/Angular replacement does not yet implement operational feature endpoints or screens.
- The retained WinUI app is the only currently implemented UI for maintenance workflows.

## Unknowns

- Real branch environment compatibility and current external-system availability.
- SQL, managed-root, and SMB access under the eventual installed Windows Service identity.
- A real interactive Negotiate/admin authorization round trip.
- Installer implementation details and offline install/upgrade/rollback results.
- Production feature parity, accessibility, and end-to-end results for the Angular replacement.
