# Stable Project Context

Read this file only when the task requires stable, non-obvious project knowledge.

## Product and Business Boundaries

- DBS POS Admin Tool administers RMS+ point-of-sale installations on one Windows branch device for a local technician/administrator.
- The retained WinUI tool configures RMS+, controls Windows services, backs up/restores SQL and files, performs guarded maintenance, and retrieves branch backup archives. A strangler migration is replacing its UI with same-origin Angular served by a privileged local Agent.
- V1 is Windows 10/11 x64, per-device, English-first, and local-only. Cloud, a central server, remote/LAN management, mobile, `win-arm64`, and role matrices are out of scope.
- Sessions 00-08 are accepted baseline architecture. Standalone Angular expansion is frozen while POS is prepared for a possible RMS+ Support Hub integration; the repositories remain separate until explicit cross-project approval.
- POS owns backend/domain/privileged Windows/SQL/SMB, contracts, Agent security, operation/audit, configuration/secrets, and portability. RMS+ Support Hub owns the final Angular shell, global navigation, shared components, visual system, branding, themes, and integrated POS route UX.
- Preserve readable legacy backup ZIPs. Import legacy non-secret settings once without modifying the legacy file; SQL and RDB passwords must be re-entered.
- The repository owns no application database or migrations. It operates on configured external RMS SQL Server databases and host files.

## Architecture Invariants

- Retained path: WinUI composes Domain, Application, and Infrastructure. Domain owns models/interfaces; Application owns workflows; Infrastructure owns SQL, Windows service, HTTP, SMB, filesystem, and configuration adapters.
- Migration path: independent versioned DTOs live in `PosAdminTool.Contracts`; `PosAdminTool.Agent` is the ASP.NET Core composition root; `PosAdminTool.Web` is an Angular standalone SPA embedded into Agent publish output.
- The Agent binds only to `127.0.0.1:5001`, uses same-origin Windows Negotiate authentication, authorizes local Administrators, and requires antiforgery validation for mutations. There is no configurable alternate bind.
- Browser contracts and errors must never reveal a secret or absolute host path. Host file selection uses allowlisted browse roots and principal/purpose-bound, single-use, five-minute opaque handles.
- Agent configuration is service-owned under `%ProgramData%\DBS\PosAdminTool`; secrets are separate and protected with machine-scope Windows DPAPI. The retained WinUI profile configuration remains separate.
- Long work belongs to a bounded in-memory Agent operation registry, outside request lifetime. REST is state truth, SSE is transport only, idempotency is principal-scoped, named locks serialize conflicts, and only destructive completions are appended to JSONL audit.
- Agent restart intentionally loses in-flight jobs and file handles. Do not add SQLite, SignalR, PWA/service-worker behavior, IndexedDB, or queued browser mutations.
- Keep WinUI buildable/runnable until explicit Session 14 parity approval. Its removal must be a dedicated cutover change.

## Build and Validation Entry Points

- Restore — discovered in CI, not run during context initialization: `dotnet restore PosAdminTool.sln --locked-mode`; from `src/PosAdminTool.Web`, `npm ci`.
- Build — Session 08 recorded passing: `dotnet build PosAdminTool.sln -c Release --no-restore` with 0 warnings / 0 errors.
- .NET tests — Session 08 recorded passing: `dotnet test PosAdminTool.sln -c Release --no-restore`, 125 tests across Domain, Application, Infrastructure, and Agent integration projects. The old Session 05 97/98 statement is historical, not current.
- Agent targeted tests — discovered in CI: `dotnet test tests/PosAdminTool.Agent.IntegrationTests/PosAdminTool.Agent.IntegrationTests.csproj -c Release`.
- Web checks — Session 08 recorded `npm --prefix src/PosAdminTool.Web run test -- --run` passing 8 tests in 6 files and a backup E2E pass; use the current session log for the exact gate scope.
- Browser tests — `npm run e2e` from `src/PosAdminTool.Web`; three Session 05 accessibility, keyboard, routing, and same-origin checks are recorded passing.
- Local Agent — discovered, not executed: `dotnet run --project src/PosAdminTool.Agent/PosAdminTool.Agent.csproj`.
- Retained WinUI — Session 08 recorded `dotnet publish src/PosAdminTool.WinUI/PosAdminTool.WinUI.csproj -c Release -r win-x64 --self-contained false --no-restore` passing; runtime validation still requires publish, not plain build.

## Integrations

- SQL Server — RMS checks, backup, restore, verification, and reset; interfaces in `src/PosAdminTool.Domain/Interfaces`, adapter in `src/PosAdminTool.Infrastructure/Windows/SqlCmdExecutor.cs`. Live service-identity access is unverified.
- Windows Service Control Manager — RMS service status/control; `src/PosAdminTool.Infrastructure/Windows/WindowsServiceManager.cs`. Future Agent operations must own privilege and report confirmed outcomes.
- RMS backup API — triggers multi-branch backup work; `src/PosAdminTool.Infrastructure/Http/BackupApiClient.cs`. Endpoint and credentials remain configuration, never shared memory.
- SMB/UNC — discovers/downloads produced archives using explicit configured credentials; `src/PosAdminTool.Infrastructure/Smb/`. Session 0 behavior under the proposed service identity is unverified.
- RMS local files — legacy import and backup sources; `src/PosAdminTool.Infrastructure/Configuration/LegacyConfigurationImporter.cs` and Application services. Import excludes secrets.
- Angular browser — same-origin UI/API client; OpenAPI is generated from Agent build using `src/PosAdminTool.Web/ng-openapi-gen.json`.

## Critical Conventions

- Exact NuGet/npm versions and lockfiles are intentional; C# stays at 13 until ADR-0013 is changed.
- `src/PosAdminTool.Web/openapi/` and `src/PosAdminTool.Web/src/app/core/api/generated/` are generated and ignored; change contracts/endpoints, then regenerate.
- Angular/OpenAPI CI uses Windows because the Agent has a Windows TFM. Published branch devices need neither Node nor npm.
- WinUI unpackaged resources are staged correctly only by publish; its project contains a required Windows App SDK XAML-resource copy workaround.
- UI status must distinguish fresh, stale, unknown, Agent-unreachable, and RMS-server-unreachable states with evidence and timestamps, not colour alone.
- The existing Angular shell uses the local-only Branch Signal Desk design system, responsive navigation down to 360 px, semantic light/dark tokens, and lazy feature routes. Session 06 replaces fixture-backed overview/device/settings surfaces with Agent data; Session 08 adds the Agent-backed backup flow. The shell and placeholder routes are retained as migration/reference material, not as a standalone feature backlog.
