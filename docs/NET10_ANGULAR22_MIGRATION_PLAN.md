# DBS POS Admin Tool — .NET 10 + Angular 22 Migration and UI Refactoring Plan

> Planning document only. No migration implementation is included.
>
> Audit date: 2026-07-26  
> Scope revision: 2026-07-26 (post-review)  
> Repository: `pos_maintainance_tool`  
> Intended implementers: GPT-5.6 Terra High, Claude Sonnet, or an equivalent coding agent working in controlled sessions

## 0. Driver, scope, and non-goals

### 0.1 Why this migration is happening

**The driver is UI modernization.** The WinUI 3 presentation layer is the complaint: its look,
its UX, and its desktop-only reach. This is not a runtime upgrade (the repository is already on
.NET 10), not a cloud programme, and not a fleet-management project.

That driver decides what is in scope. It justifies treating Section 8 (the "Branch Signal Desk"
design system) as the **centerpiece deliverable**. It removes the justification for durable
persistence, offline-first behavior, and multi-device access. It does **not** remove the
HTTP-boundary safety work — moving privileged Windows/SQL/SMB operations onto a network protocol
creates a new attack surface regardless of why the UI is being rebuilt.

### 0.2 Measured baseline

Any scope decision has to be weighed against how much code actually exists:

| Project | Files | Lines of C# |
| --- | --- | --- |
| `PosAdminTool.Domain` | 21 | 419 |
| `PosAdminTool.Application` | 8 | 1,089 |
| `PosAdminTool.Infrastructure` | 11 | 1,115 |
| `PosAdminTool.WinUI` | 21 | 1,575 (+ 11 XAML files) |
| **Total** | **61** | **~4,200** |

Existing tests: 14 `[Fact]`/`[Theory]` methods across 8 files in 3 xUnit projects.

The tool being replaced is roughly 4,200 lines. Every added subsystem must be justified against
that number, not against what a large enterprise product would carry.

### 0.3 Deliberately cut from v1

These were in the first draft of this plan and have been removed after review. Each is a
*deferral*, not a rejection — any of them can return as a self-contained later increment.

| Cut | Replaced by | Why this is safe |
| --- | --- | --- |
| SQLite persistence, schema, and migrations | In-memory bounded job registry in the agent, plus an append-only JSONL audit file for destructive operations only | The current WinUI app already loses all job and log state on restart (§3.3; `LogHub` is a 1,000-entry memory ring). The genuinely new failure mode introduced by a browser is **tab refresh**, and agent-side memory already solves that, because the agent is a long-lived service. No schema, no migrations, no migration test matrix. |
| SignalR hub and client library | Server-Sent Events (`text/event-stream`) for progress; plain REST for initial load and rehydration | The draft required SignalR *and* full REST rehydration, so two update paths would be built and tested. `EventSource` handles reconnect natively in every target browser. |
| PWA, service worker, IndexedDB offline stores | An honest "agent unreachable" state in the shell | Self-defeating on loopback: the agent is the thing that serves the UI. §2 concedes that offline-without-agent yields a cached shell and nothing operable. Large cost, near-zero user value. |
| LAN mode, device pairing, revocation, Viewer/Operator/Administrator matrix | Loopback-only bind plus Negotiate authentication restricted to the local Administrators group | Not the driver. One technician, one device, one local operator. |
| Legacy encrypted-secret migration | Migrate non-secret configuration; prompt once for the two passwords on first run | The existing key derivation is bound to the **interactive user** identity. A Windows Service account cannot decrypt that ciphertext, and a bootstrap step would have to run in the original user's context. For exactly two secrets, re-entry removes an entire class of risk. |
| SBOM generation, vulnerability scanning, multi-day soak tests, Storybook, `win-arm64` package | — | Enterprise ceremony disproportionate to a single-branch maintenance tool. |

### 0.4 Non-negotiable, because HTTP exposure forces it

Cutting scope must not cut safety. The following are **required** and are not subject to
trimming for schedule:

- Destructive operations require: preview → server-issued expiring one-time challenge → typed
  confirmation → **policy recomputed at execute time** → audit record. A stale or reused
  challenge fails closed.
- Canonical path policy with managed roots and a protected-root denylist. This is the fix for a
  real defect: `CleanupService` currently expands environment variables and calls
  `Directory.Delete(recursive: true)` with no allowlist, so a configured value of `C:\` would be
  honored.
- The full archive-hardening list (§5.4).
- Secret contract: `hasSqlPassword` / `hasRdbPassword` flags, blank means keep, clearing is a
  separate authorized operation, and a secret is never returned to the browser.
- Loopback-only default bind, same origin, no permissive CORS, antiforgery on all mutations,
  strict Content Security Policy.
- Problem Details with correlation IDs, opaque IDs in public contracts, UTC persisted timestamps.
- Strangler sequencing: WinUI stays until a dedicated final removal commit after parity approval.

### 0.5 Explicit non-goals

- No cloud service, no central server, no public internet exposure, no port forwarding.
- No fleet or multi-device orchestration. One agent manages one device.
- No offline-without-agent functionality beyond a shell that says so.
- No remote/LAN access in v1.
- No support for restoring or reading secrets created by the WinUI-era config format.

### 0.6 Honest effort expectation

Fifteen sessions (00–14). A session is a reviewable unit of work, **not** a single sitting —
several are multi-day with human review. Do not read "15 sessions" as "15 hours".

## 1. Executive decision

The repository is already on .NET 10. The required conversion is therefore not a .NET runtime upgrade; it is a presentation and hosting migration:

- Keep and harden the existing .NET 10 Domain and Application logic.
- Replace the WinUI 3 presentation project with an Angular 22 web application.
- Add an ASP.NET Core 10 local management API/agent that owns all privileged Windows, SQL Server, SMB, file-system, and long-running work.
- Serve the compiled Angular application from that same .NET host for a same-origin deployment.
- Install the host as a self-contained Windows Service on each RMS/POS device.
- Support an internet-free local mode on every installed device.
- Keep the existing WinUI application available until functional and operational parity has been proven.

LAN/remote access is a deliberate post-v1 increment (§0.3). The agent must never be
port-forwarded or otherwise exposed to the public internet.

An Angular application running in a browser cannot directly control Windows services, open SQL Server backups on the host, use SMB credentials, or perform elevated cleanup. Those operations must remain server-side in the local Windows agent.

## 2. Definition of “offline”

“Offline” means **no internet or cloud dependency**. It does not mean “all functions work when
the UI cannot reach an agent” — that is not achievable and is not promised.

| Scenario | Supported behavior |
| --- | --- |
| Browser and agent on the same Windows POS device | Full functionality through `http(s)://localhost:<port>`; internet is not required. This is the only supported topology in v1. |
| Browser cannot reach the agent | The shell reports “agent unreachable” and disables every host operation. No cached operational data is presented as current. |
| POS device is disconnected from the main RMS server | Local service control, local configuration, local backup, restore, and cleanup continue to work. Branch verification and DB Downloader operations must report “main server unavailable.” |
| Browser requests a destructive action while disconnected | The action is rejected. Destructive work is **never** queued for later replay. |

### Device support boundary

- The management agent is Windows-only because RMS service control, administrative SMB access, local file paths, and elevation are Windows-specific.
- The runtime target is `win-x64`, matching the current application and expected RMS estate.
- The Angular UI must work in the Chromium-based browser present on the POS device. Cross-browser and mobile support is a consequence of using the web platform, not a v1 requirement, and is not separately tested.

## 3. Current-state audit

### 3.1 Solution inventory

| Project | Current target | Purpose | Migration treatment |
| --- | --- | --- | --- |
| `PosAdminTool.Domain` | `net10.0` | Models, enums, interfaces | Retain; remove secret defaults and evolve domain contracts. |
| `PosAdminTool.Application` | `net10.0` | Backup, restore, cleanup, import, verification, download orchestration | Retain and refactor host/file-system dependencies behind ports. |
| `PosAdminTool.Infrastructure` | `net10.0-windows10.0.19041.0` | SQL Server, Windows services, SMB, encrypted JSON config, HTTP connectivity | Split or organize into portable persistence/configuration and Windows-specific adapters. |
| `PosAdminTool.WinUI` | `net10.0-windows10.0.19041.0` | WinUI shell, pages, MVVM state, theme and activity log | Freeze after parity baseline; remove only at final cutover. |
| Three xUnit projects | .NET 10 | 14 test methods/theories, including DB downloader, import, crypto, connectivity and domain helpers | Preserve and expand substantially. |

There is no Angular workspace, Node lockfile, ASP.NET Core host, installer project, `global.json`, or CI pipeline in the audited repository. Solution file: `PosAdminTool.sln` (4 source + 3 test projects). See §0.2 for measured file and line counts.

### 3.2 Existing feature map

| Existing area | Behavior that must survive |
| --- | --- |
| Configuration | Load/save settings, SQL connectivity test, branch verification, RMS+ import, connectivity state, light/dark preference. |
| Services | List configured Windows services, poll status, start, stop, and restart each service. |
| Operations — backup | Select branch DB, cashier DB, and three appsettings files; create SQL backups; stage and ZIP artifacts. |
| Operations — restore | Full, database-only, or config-only restore; inspect backup logical files; restore with `MOVE`; overwrite known appsettings destinations. |
| Operations — cleanup | Stop configured services and recursively delete configured folders. |
| Operations — reset | Delete branch data from known SQL tables for the configured branch. |
| DB Downloader | Trigger a main-server backup batch, find the newest created batch folder over SMB, validate stable branch ZIPs, and download each ready result. |
| Log | In-memory timestamped activity console with a 1,000-entry cap. |

### 3.3 Platform and presentation coupling

- WinUI view models depend on `DispatcherQueue`, `DispatcherQueueTimer`, XAML commands, and `LogHub`.
- The application starts elevated and remains elevated for its entire desktop lifetime.
- SQL operations use `Microsoft.Data.SqlClient`.
- Windows service management uses `System.ServiceProcess.ServiceController` and `sc.exe`.
- SMB access uses `mpr.dll` through `WNetAddConnection2`/`WNetCancelConnection2`.
- Backup logic opens Explorer through `Process.Start`.
- Import, backup, restore, and cleanup directly use host file-system paths.
- DB Downloader jobs and logs live only in UI memory and are lost on restart.
- Browser file semantics do not match desktop text-path inputs. Restore and download flows need explicit streaming/catalog APIs.

### 3.4 Risks to resolve during migration

These are release blockers, not optional refactors. Every item below was verified against source
on 2026-07-26; the cited location is the evidence. **Re-verify each before acting on it** — do not
trust this table if the code has since moved.

| # | Finding | Evidence | Session |
| --- | --- | --- | --- |
| 1 | `AppSettings.SqlPassword` has the hard-coded default `"P@ssw0rd"`. New installations must default to an empty secret. | `src/PosAdminTool.Domain/Models/AppSettings.cs:13` | 03 |
| 2 | `ConfigurationService` encrypts/decrypts `SqlPassword` but **not** `DbDownloader.RdbPassword`, despite `README.md:37` stating both are encrypted. Documentation and behavior diverge. | `src/PosAdminTool.Infrastructure/Configuration/ConfigurationService.cs:114,119` vs `src/PosAdminTool.Domain/Models/DbDownloaderSettings.cs:11` | 03 |
| 3 | Existing key derivation is bound to machine **and interactive-user** identity, so a Windows Service account cannot decrypt existing ciphertext. Legacy secrets are therefore **not migratable**; see §5.5 for the re-entry decision. | `src/PosAdminTool.Infrastructure/Configuration/` crypto path | 03 |
| 4 | A hard-coded environment-specific endpoint ships in domain defaults: `http://10.10.9.181:8080/rmsmainserverApi/...`. | `src/PosAdminTool.Domain/Models/DbDownloaderSettings.cs:5` | 03 |
| 5 | Cleanup expands environment variables and calls `Directory.Delete(recursive: true)` on configured paths with **no** allowlist, protected-root check, preview, or server-issued token. A configured value of `C:\` would be honored. | `src/PosAdminTool.Application/Services/CleanupService.cs:31-48` | 11 |
| 6 | The danger-zone checkbox is entirely client-side and becomes meaningless once operations are reachable over HTTP. | `src/PosAdminTool.WinUI/ViewModels/` | 11 |
| 7 | The downloader accepts a configurable HTTP endpoint and SMB host. The agent must defend against SSRF, unsafe schemes, unapproved destinations, and credential disclosure. | `src/PosAdminTool.Application/Services/DbDownloadService.cs:18,106` | 12 |
| 8 | ZIP restore needs entry-count, expanded-size, compression-ratio, traversal, file-type, and upload-size limits. None exist. | `src/PosAdminTool.Application/Services/RestoreService.cs` | 09 |
| 9 | Long operations need IDs, cancellation, and concurrency control so they survive **browser refresh**. Surviving agent restart is explicitly *not* required — the current app loses all job state on exit, so in-memory job state in a long-lived service already exceeds parity (§0.3). | `src/PosAdminTool.WinUI/ViewModels/DbDownloaderViewModel.cs` | 04 |
| 10 | API responses and logs must never return passwords, connection strings, tokens, SMB credentials, or raw exception internals. | new surface | 02, 03 |
| 11 | Only 14 test methods exist. Nothing covers backup, restore, cleanup safety, service control, API authorization, or file streaming. | `tests/` | all |
| 12 | Wildcard NuGet versions prevent deterministic offline rebuilds: `Microsoft.Data.SqlClient 6.*`, `Microsoft.Extensions.* 10.*`, `Microsoft.WindowsAppSDK 1.8.*`, `CommunityToolkit.Mvvm 8.*`. | 4 `.csproj` files | 01 |
| 13 | The repository runs a .NET 10 SDK but pins `<LangVersion>13.0</LangVersion>`. Deliberately select C# 14 or record why C# 13 stays. | `Directory.Build.props:3` | 01 |
| 14 | `WNetAddConnection2` maps SMB connections in the caller's logon session. Under a Windows Service in session 0 this behaves differently from the current interactive elevated process. This is a known trap and a genuine migration risk. | `src/PosAdminTool.Infrastructure/Smb/SmbConnectionScope.cs:66` | 12 |
| 15 | Backup opens Explorer via `Process.Start` for the technician. A server cannot do this; the UI needs a replacement affordance (§8.7). | `src/PosAdminTool.Application/Services/BackupService.cs:276` | 08 |

## 4. Target product and architecture

### 4.1 Product definition

- **Subject:** a branch-level RMS/POS maintenance console.
- **Primary audience:** field technicians and support engineers working under time pressure on a specific branch device.
- **Single job:** safely understand the current device state and execute an auditable maintenance operation without losing branch context.

### 4.2 Runtime topology

```text
  Windows POS device — single device, loopback only
  ┌─────────────────────────────────────────────────────────────────────────┐
  │ Local browser                                                           │
  │  - Angular 22 standalone app                                            │
  │  - locally bundled fonts / icons                                        │
  │  - no service worker, no offline store                                  │
  └──────────────────────────────┬──────────────────────────────────────────┘
                                 │ same origin, 127.0.0.1 only
                                 │ REST for state · SSE for progress
  ┌──────────────────────────────▼──────────────────────────────────────────┐
  │ PosAdminTool.Agent — ASP.NET Core 10 Windows Service                    │
  │  REST /api/v1 │ SSE │ Negotiate auth │ in-memory job registry │         │
  │  resource locks │ static Angular files │ JSONL destructive audit        │
  └───────────┬─────────────────┬────────────────┬─────────────────┬────────┘
              │                 │                │                 │
       Windows services     SQL Server      Local files       Main server
       (ServiceController,  (Microsoft.     (managed roots,    (HTTP trigger
        sc.exe)              Data.SqlClient) allowlisted       + SMB fetch)
                                             browse roots)
```

Job state lives in agent memory and therefore survives browser refresh but not agent restart.
That is a deliberate, documented parity decision (§0.3). Only completed destructive operations
are appended to an on-disk JSONL audit file.

### 4.3 Proposed repository shape

```text
src/
  PosAdminTool.Domain/                # existing, unchanged shape
  PosAdminTool.Application/           # existing, refactored behind ports
  PosAdminTool.Contracts/             # NEW — versioned HTTP DTOs only
  PosAdminTool.Infrastructure/        # existing; Windows/ subfolder keeps the boundary
  PosAdminTool.Agent/                 # NEW — ASP.NET Core 10 host + Windows Service
  PosAdminTool.Web/                   # NEW — Angular 22 workspace (incl. e2e/)
  PosAdminTool.WinUI/                 # retained until approved cutover

tests/
  PosAdminTool.Domain.Tests/          # existing
  PosAdminTool.Application.Tests/     # existing
  PosAdminTool.Infrastructure.Tests/  # existing
  PosAdminTool.Agent.IntegrationTests/  # NEW

installer/
  PosAdminTool.Installer/             # NEW, Session 14 only

docs/
  adr/
  migration/
```

Three deliberate simplifications versus a larger-project layout:

- **No separate `Infrastructure.Windows` project.** Windows-only code stays in an
  `Infrastructure/Windows/` folder, which is where it already lives. The boundary that matters is
  that Windows-only code never reaches Angular or the Contracts assembly — a project split does
  not enforce that any better than review does, and the agent is Windows-only by design anyway.
- **No `PosAdminTool.Web.Tests` project.** Angular unit tests live inside the Angular workspace,
  which is how the Angular toolchain expects it.
- **No `PosAdminTool.EndToEndTests` project.** Playwright specs live in `src/PosAdminTool.Web/e2e/`.

### 4.4 Architectural rules

- Domain has no UI, ASP.NET Core, SQL client, Windows, or file-system implementation dependencies.
- Application owns use-case orchestration and policies, not HTTP concerns.
- Contracts contain versioned request/response DTOs and event payloads, never internal entities or secrets.
- Infrastructure implements SQL Server, secure secret storage, SMB, Windows services, connectivity, and managed file access.
- Agent owns authentication, authorization, validation, endpoint mapping, background jobs, hosting, and static Angular delivery.
- Web owns presentation state only. It never writes SQL or RDB credentials to any browser storage, URL, log, or generated diagnostic.
- All mutation endpoints are explicit commands. No state-changing `GET` endpoints.
- Destructive commands require a fresh server-side preview and confirmation challenge.
- Long operations execute outside the HTTP request lifetime.

## 5. Backend design

### 5.1 ASP.NET Core agent

Create a .NET 10 Web SDK host that:

- runs interactively in development and as a Windows Service in production;
- **binds to `127.0.0.1` only** — there is no configuration path to a non-loopback bind in v1;
- serves the Angular production build and `/api/v1` from one origin;
- authenticates with Negotiate (Windows Integrated) and authorizes a single principal: membership
  of the local Administrators group. There is no role matrix in v1;
- enforces antiforgery on every mutation and a strict Content Security Policy;
- exposes `/health/live`, `/health/ready`, and an authenticated capability endpoint the shell uses
  to detect an incompatible agent;
- streams operation progress over **Server-Sent Events** (`text/event-stream`); the browser's
  native `EventSource` provides reconnect, so no hub or client library is required;
- keeps a **bounded in-memory job registry** with per-operation cancellation. Job state survives
  browser refresh because the agent outlives the tab; it does not survive agent restart, and the
  UI must say so plainly rather than imply otherwise;
- prevents conflicting operations with named resource locks: `sql`, `services`,
  `filesystem-cleanup`, and `downloader`;
- appends every completed destructive operation (cleanup, reset, overwrite restore) to an
  **append-only JSONL audit file** under `%ProgramData%\DBS\PosAdminTool\audit\`, with restrictive
  ACLs. No database, no schema, no migrations;
- returns RFC 9457 Problem Details with a correlation ID and a safe user-facing message;
- never exposes developer exception pages outside Development.

Explicitly **not** in the agent: SQLite, SignalR, restart-resume logic, an `Interrupted` recovery
workflow, device pairing, or a role matrix. Each was removed for a stated reason in §0.3.

### 5.2 API outline

Exact schemas belong in OpenAPI and `PosAdminTool.Contracts`.

| Area | Suggested endpoints |
| --- | --- |
| Session | `GET /api/v1/session` (current principal + capability/version metadata) |
| Device | `GET /api/v1/device`, `GET /api/v1/device/capabilities`, `GET /api/v1/device/connectivity` |
| Configuration | `GET /api/v1/configuration`, `PUT /api/v1/configuration`, `POST /api/v1/configuration/import-rms`, `POST /api/v1/configuration/test-database` |
| Branch | `POST /api/v1/branches/verify` |
| Services | `GET /api/v1/services`, `POST /api/v1/services/{serviceId}/actions` |
| Operations | `POST /api/v1/operations`, `GET /api/v1/operations`, `GET /api/v1/operations/{id}`, `POST /api/v1/operations/{id}/cancel` |
| Local backups | `POST /api/v1/backups`, `GET /api/v1/backups`, `GET /api/v1/backups/{id}/download` |
| Restore | `POST /api/v1/restores/uploads`, `POST /api/v1/restores/preview`, `POST /api/v1/restores/execute` |
| Cleanup/reset | `POST /api/v1/maintenance/cleanup/preview`, `POST /api/v1/maintenance/cleanup/execute`, `POST /api/v1/maintenance/reset/preview`, `POST /api/v1/maintenance/reset/execute` |
| DB Downloader | `GET/PUT /api/v1/downloader/settings`, branch catalog endpoints, job endpoints, and streamed result downloads |
| Host files | `POST /api/v1/files/browse`, `POST /api/v1/files/handles` — see §5.7. Required for restore source selection and backup destination selection. |
| Activity | `GET /api/v1/activity`, `GET /api/v1/activity/export` |
| Real-time | `GET /api/v1/events` — one SSE stream carrying operation status, progress, connectivity, and service-state events. Sanitized payloads only. |

Use opaque IDs in URLs. Never accept a raw file-system path, service name, remote ZIP path, or SMB share path from an untrusted browser request when a server-issued ID can be used.

### 5.3 Operation model

Every long-running command should have:

- operation ID (opaque) and type;
- branch snapshot taken at queue time;
- requesting principal;
- requested / start / end timestamps in UTC;
- state: `Queued`, `Running`, `Succeeded`, `PartiallySucceeded`, `Failed`, or `Cancelled`;
- monotonic progress and current stage;
- sanitized event messages;
- owned resource locks;
- result artifact IDs, never raw paths;
- safe error code and correlation ID.

There is no `Interrupted` state and no resumability classification. Agent restart clears the
registry, which matches the behavior of the application being replaced.

SSE is the live update path. A `GET /api/v1/operations/{id}` read is the rehydration path after a
refresh or a dropped stream. The Angular app must rehydrate from REST on load and on SSE reopen,
and must **never** re-issue a command as part of recovery — recovery is read-only.

### 5.4 Files and archives

- Introduce managed roots for backups, uploads, restore staging, downloads, logs, and database files.
- Canonicalize every path and verify it remains under its configured managed root.
- Reject root drives, user profiles, Windows directories, application install directories, and unresolved environment-variable paths from cleanup.
- Store server paths internally; expose artifact metadata and IDs to the UI.
- Stream uploads and downloads with size limits and cancellation. Do not buffer entire database archives in memory.
- Validate ZIP entry paths, count, total expanded size, compression ratio, file extensions, duplicate names, and destination mappings before extraction.
- Create a restore preview that lists the selected `.bak`, logical SQL files, config destinations, overwrite behavior, required disk space, and affected services.
- Keep the old archive naming convention readable while introducing a manifest file with schema version, branch, POS, release, creation time, checksums, and included components.

### 5.5 Configuration and secrets

- Import **non-secret** configuration once from `%USERPROFILE%\.pos_admin_tool\config.json`
  (branch code, POS number, paths, service list, server addresses, known branches).
- **Do not attempt to migrate the two encrypted secrets.** The legacy key derivation is bound to
  the interactive user identity, so the service account cannot decrypt them (§3.4 item 3). Instead,
  detect that secrets are absent and require the technician to enter the SQL and RDB passwords once
  on first run. This is a deliberate one-time cost that removes an entire class of failure.
- Never modify or delete the legacy `config.json`. Read it, and leave it untouched as a fallback
  for as long as WinUI is retained.
- Move non-secret configuration to a versioned, service-owned JSON file under
  `%ProgramData%\DBS\PosAdminTool`.
- Store SQL and SMB secrets using Windows Data Protection scoped to the **service account**, or
  Windows Credential Manager with ACLs matching the service identity.
- Encrypt both SQL and RDB passwords.
- Never return an existing password to Angular. Return `hasSqlPassword` and `hasRdbPassword`; blank secret fields mean “keep current secret.”
- Give secret replacement and secret clearing distinct API semantics.
- Remove hard-coded passwords and environment-specific IPs from domain defaults.
- Make writes atomic using a temporary file, flush, replace, and backup sequence where files remain involved.
- Restrict configuration file ACLs to Administrators and the service identity.

### 5.6 Service identity and privilege

This is the single most consequential decision in the migration, because it changes the identity
that performs every SQL, SMB, and file operation. The current application runs elevated as the
interactive technician; the agent will not. Decide and document it in an ADR **before** feature work:

- Prefer a dedicated local service account holding only the rights actually required: log-on-as-a-service,
  read/write on the managed roots, and whatever SQL Server login the branch database needs.
- If LocalSystem is chosen, document the consequences explicitly for SQL login mapping, SMB
  outbound identity, file ACLs, and network authentication.
- **Validate SMB early.** `WNetAddConnection2`
  (`src/PosAdminTool.Infrastructure/Smb/SmbConnectionScope.cs:66`) maps a connection into the
  caller's logon session. Under a service in session 0 this behaves differently from an
  interactive elevated process, and it is a known source of failure. Prove the DB Downloader's SMB
  path works under the real service identity in Session 12 rather than assuming it.
- **Validate SQL early.** The branch database login currently succeeds as the technician. Confirm
  the service account can authenticate before building UI on top of it (Session 06).
- Do not elevate the browser. The browser is an unprivileged client of a privileged service.
- v1 authorizes exactly one principal: a member of the local Administrators group. There is no
  Viewer/Operator/Administrator matrix (§0.3).

### 5.7 Host file access — selecting files that already exist on the device

This section closes a gap that would otherwise surface as a blocker during the restore work.

The rule in §5.2 — never accept a raw file-system path from the browser — is correct, but the two
replacements offered elsewhere in this plan are insufficient on their own:

- **Streamed upload** works for a config file. It is absurd for a multi-gigabyte `.bak` that is
  already sitting on the same machine as the agent, or on a USB stick plugged into it.
- **A managed-artifact catalog** only ever contains backups this tool itself produced. It cannot
  contain the archive a technician just copied from a share.

The real field workflow is *"the `.bak` is on `D:\`, or on a USB stick, or on a share I just
mounted."* A third mechanism is required:

**Allowlisted server-side browse with opaque handles.**

- Configuration defines a set of **browse roots** — for example the managed backup root, a
  removable-media root, and any operator-approved additional roots. Roots are configuration, not
  request input.
- `POST /api/v1/files/browse` accepts a **root ID plus a relative sub-path** and returns directory
  entries with name, size, and last-modified time. It never accepts or returns an absolute path.
- Every resolved path is canonicalized and re-checked to be inside its declared root **after**
  resolution, so `..` traversal, symlinks, junctions, and unresolved environment variables cannot
  escape. Reparse points are rejected rather than followed.
- `POST /api/v1/files/handles` exchanges a browsed entry for a short-lived **opaque handle**.
  Restore, backup destination selection, and archive inspection accept only handles, never paths.
- Handles are single-purpose, expiring, bound to the issuing principal, and re-validated at use
  time — a handle is not a capability to read arbitrary bytes later.
- Browse results are a read operation and must never enumerate outside the configured roots, even
  if a root is misconfigured to a broad location; the protected-root denylist from §6.3 applies to
  browse as well as to deletion.

The same handle mechanism serves the backup **destination** picker, which has the identical
problem in reverse.

## 6. Security model

### 6.1 Default local mode

- Bind only to loopback.
- Serve UI and API from one origin; do not enable permissive CORS.
- Prefer Windows Integrated Authentication for supported Windows clients.
- Use short-lived, secure, HTTP-only, `SameSite=Strict` sessions where a web session is needed.
- Enforce antiforgery tokens on cookie-authenticated mutations.
- Apply a strict Content Security Policy and `frame-ancestors 'none'`.

### 6.2 Remote access — out of scope for v1

LAN/remote access, device pairing, and per-device roles are **not built in v1** (§0.3). The agent
binds to `127.0.0.1` and there is no configuration path to change that.

Two rules protect the deferral:

- No code, configuration key, or documentation may imply a non-loopback bind is available. A
  security test must assert that no non-loopback listener exists.
- The agent must never be port-forwarded or reverse-proxied to the public internet. If LAN access
  is added later it requires, at minimum: mandatory HTTPS with a per-device certificate, displayed
  fingerprint and trust instructions, short-lived single-use pairing codes, hashed and revocable
  paired-device credentials, rate limiting on pairing and on every sensitive command, and a role
  matrix. That is a self-contained increment, not a configuration flag.

### 6.3 Dangerous operation controls

For cleanup, reset, and overwrite restore:

1. The client requests a preview.
2. The server evaluates current configuration and returns an expiring challenge ID, exact impact list, branch/device identity, and confirmation phrase.
3. The UI displays the impact without preselected acceptance.
4. The operator types the branch code or supplied phrase.
5. The execute request sends the challenge ID and confirmation.
6. The server recomputes policy, verifies the challenge is unused and unexpired, checks authorization, acquires locks, and records the audit event.

Preview and execute must be separate requests. A stale or changed preview must fail closed.

## 7. Angular 22 application design

### 7.1 Technical baseline

- Angular 22 standalone components and route-level lazy loading.
- **Verify the Node.js and TypeScript support matrix directly against `https://angular.dev/reference/versions`
  during Session 01.** This document previously asserted TypeScript `>=6.0.0 <6.1.0`; treat that as
  unverified and confirm it before pinning. Pin the exact Node and package-manager versions in
  repository files once confirmed.
- A committed lockfile and exact production dependency versions. No wildcard or range specifiers.
- Signals for local feature state and computed UI state.
- RxJS for HTTP, the SSE event stream, cancellation, and multi-event streams.
- Typed reactive forms for configuration and maintenance workflows.
- Angular CDK for accessibility primitives, overlays, focus management, and responsive utilities.
- A generated, versioned API client from the OpenAPI document.
- The Angular 22 default unit runner unless the Session 01 tooling decision selects otherwise.
- Playwright for the five critical browser journeys named in §10.3.
- No CDN fonts, CDN icons, runtime npm downloads, cloud analytics, or internet-hosted assets.
- No service worker, no PWA manifest, no IndexedDB (§0.3).

Avoid adding a global state framework. Feature stores built from services and signals are
sufficient for this scope and nothing here will outgrow them.

> Note on the Angular 22 choice: v22 is a very recent major, and Angular majors carry an ~18-month
> support window — shorter than .NET 10's LTS window (November 2028). The version is a deliberate
> requirement, but plan for one Angular major upgrade inside the supported life of this tool.

### 7.2 Feature structure

```text
src/app/
  core/
    api/            # generated client + interceptors
    auth/
    connectivity/   # agent reachability + API compatibility
    errors/
    realtime/       # SSE client
  layout/
  shared/
    ui/
    directives/
    pipes/
  features/
    overview/
    device/
    configuration/
    services/
    backups/
    restore/
    maintenance/
    downloader/
    activity/
    settings/
```

Every feature should include its routes, page/component files, feature state, tests, and domain-to-view adapters together.

### 7.3 Connectivity, freshness, and honest degradation

There is no service worker, no offline cache, and no IndexedDB (§0.3). The agent serves the UI, so
if the agent is unreachable there is nothing to manage — the correct behavior is to say so clearly,
not to simulate a working application.

What the UI must still do well:

- Distinguish `fresh`, `stale`, and `unknown` for every piece of polled state, visibly and not by
  color alone.
- Show a last-checked timestamp beside any status that can age — `Last checked 14:32:08`, never a
  bare `Online`.
- Detect agent unreachability and API version incompatibility, and **disable every host mutation**
  while either is true, with a plain explanation rather than a spinner.
- Distinguish *the agent is unreachable* from *the agent is fine but the main RMS server is
  unreachable*. These have completely different remedies and the current app conflates them.
- Never present aged data as current, and never queue a command for later delivery. A rejected
  command must be visibly rejected.
- Keep in-memory state only. Unsaved form drafts may live in component state; they do not need to
  survive a reload, and secrets must never be written to any browser storage.

## 8. Magnificent modern UI refactoring

### 8.1 Design direction: “Branch Signal Desk”

The product should feel like a precise field-service instrument, not a consumer SaaS dashboard. Its visual language comes from branch topology, terminal diagnostics, service states, and POS hardware labels.

The memorable element is a **live branch signal path**:

```text
[This device P087 / POS 03] ── [RMS services] ── [Local SQL] ── [Main server]
        READY                     2 / 3 RUNNING      READY           OFFLINE
```

It appears as the overview thesis and collapses into a compact status strip elsewhere. Each node is keyboard reachable, describes its evidence and timestamp, and routes to the relevant diagnostic area.

This replaces the generic “hero plus four metric cards” pattern in the current WinUI pages. The rest of the interface stays quiet so the signal path remains the single strong visual idea.

### 8.2 Core visual tokens

Primary light theme:

| Token name | Value | Use |
| --- | --- | --- |
| Porcelain | `#F3F6F8` | Canvas |
| Paper | `#FFFFFF` | Work surfaces |
| Terminal ink | `#142130` | Primary text |
| DBS cobalt | `#2457D6` | Primary action and focus |
| Healthy teal | `#008B83` | Healthy/connected states |
| Service amber | `#B86A00` | Attention and transitional states |
| Fault red | `#BD3044` | Failure and destructive action |
| Instrument line | `#CED7E0` | Dividers and topology rails |

Dark theme equivalents:

| Token name | Value |
| --- | --- |
| Night canvas | `#0E1722` |
| Night panel | `#152231` |
| Night ink | `#F3F7FA` |
| Night muted | `#9FAFBE` |
| Cobalt light | `#7EA3FF` |
| Teal light | `#42C8B8` |
| Amber light | `#FFC166` |
| Fault light | `#FF7185` |
| Night line | `#2B4155` |

All semantic combinations must be verified against WCAG 2.2 AA. Status must never rely on color alone.

### 8.3 Typography

- Display/device identity: locally bundled **Barlow Condensed**, semibold.
- Body and controls: locally bundled **Source Sans 3**, variable where supported.
- Logs, codes, paths, IDs, and timestamps: locally bundled **IBM Plex Mono**.
- System fallbacks must be declared for each stack.
- Verify font licenses and include required notices in the distribution.

The condensed display face is reserved for branch identity, release, and key operational headings. It must not be used for long text.

### 8.4 Shape, spacing, iconography, and motion

- Use a 4 px base spacing system with primary steps of 8, 12, 16, 24, 32, and 48 px.
- Use 4 px radii for controls, 8 px for work panels, and 12 px only for major dialogs. Avoid making every element a pill.
- Use thin topology lines and small square/diamond status markers derived from service diagrams.
- Bundle a consistent outlined icon set locally; do not use emoji or mixed icon families.
- Use one orchestrated connection-check animation on initial load and restrained state transitions afterward.
- Respect `prefers-reduced-motion`; status must remain understandable with all motion removed.
- Minimum touch target: 44 by 44 CSS pixels.

### 8.5 Shell wireframes

Desktop:

```text
┌───────────────┬───────────────────────────────────────────────────────────┐
│ DBS           │ P087 · POS 03                         OFFLINE / DARK / USER│
│ POS ADMIN     ├───────────────────────────────────────────────────────────┤
│               │ Device ─ Services ─ SQL ─ Main server                    │
│ Overview      ├───────────────────────────────────────────────────────────┤
│ Device        │                                                           │
│ Services      │ Route work area                                           │
│ Backups       │                                                           │
│ Restore       │                                              Activity rail │
│ Downloads     │                                                           │
│ Activity      │                                                           │
│ Settings      │                                                           │
└───────────────┴───────────────────────────────────────────────────────────┘
```

Phone:

```text
┌──────────────────────────────┐
│ P087 / POS 03        OFFLINE │
│ Device—Svc—SQL—Main          │
├──────────────────────────────┤
│ Route title                  │
│                              │
│ Single-column work area      │
│                              │
├──────────────────────────────┤
│ Home  Services  Ops  More    │
└──────────────────────────────┘
```

### 8.6 Information architecture

1. **Overview** — branch signal path, active operation, actionable issues, recent completed work.
2. **Device** — branch/POS/release identity, local agent details, connectivity evidence, configuration summary.
3. **Services** — grouped service states, refresh age, start/stop/restart actions, per-service outcome.
4. **Backups** — component selection, destination, estimated impact, progress, artifact catalog and download.
5. **Restore** — upload or managed artifact selection, archive inspection, target mapping, preview, confirmation and progress.
6. **Maintenance** — cleanup and branch reset, separated from routine operations and protected by preview challenges.
7. **Downloads** — branch catalog, main-server job trigger, batch state and ready artifacts.
8. **Activity** — durable filterable operation/audit timeline with export.
9. **Settings** — general configuration, SQL, paths, main server, service list, browse roots, and appearance. No remote-access or paired-device sections in v1.

### 8.6.1 WinUI → Angular screen parity map

Because UI modernization is the driver (§0.1), visual and functional parity must both be
checkable. Every current WinUI page maps to a target route, and no current capability may be lost
without an explicitly accepted entry in the parity matrix.

| Current WinUI page | Target route(s) | Notes on the change |
| --- | --- | --- |
| `ConfigurationPage` | `/settings` + `/device` | Split: editable settings vs read-only device identity and connectivity evidence. Secret fields become keep/replace/clear. |
| `ServicesPage` | `/services` | Polling moves server-side; `DispatcherQueueTimer` is replaced by SSE plus a REST refresh. |
| `OperationsPage` (backup) | `/backups` | Gains a managed artifact catalog and a browse-handle destination picker. Loses the automatic Explorer open (§8.7). |
| `OperationsPage` (restore) | `/restore` | Source selection becomes upload **or** browse handle (§5.7), plus a mandatory server-side preview. |
| `OperationsPage` (cleanup/reset) | `/maintenance` | Moved off the routine-operations page entirely. Client checkbox replaced by server challenge + typed confirmation. |
| `DbDownloaderPage` | `/downloads` | SMB details and RDB credentials stop being visible to the UI; artifacts arrive by ID. |
| `LogPage` | `/activity` | Still in-memory and still capped, matching current behavior. Gains filtering, correlation-ID copy, and sanitized export. |
| *(none)* | `/` overview | New. The branch signal path (§8.1) — the one genuinely new screen. |

The three WinUI concepts with no target and no replacement — `LogHub`'s `DispatcherQueue`
marshalling, `DispatcherQueueTimer` polling, and process-wide elevation — are infrastructure, not
features, and their removal needs no parity entry.

### 8.7 Screen-specific requirements

#### Overview

- Lead with the signal path, not decorative metrics.
- Show one primary recommended action when something is unhealthy.
- Show current branch identity at all times.
- Show active operation progress with a route back to its details.
- Empty state: “No maintenance has run on this device yet” with a link to diagnostics, not decorative copy.

#### Services

- Use a compact table/list on desktop and stacked rows on mobile.
- Include service display name, internal name, state, last checked, and allowed actions.
- Optimistically indicate “command sent,” but do not claim success until the agent confirms it.
- Disable conflicting commands while a service transition is active.

#### Backups and restore

- Use a step-based workflow only because the work has a real sequence: select, review, run, result.
- Keep the selected branch and target database visible at the review/confirmation step.
- Offer two clearly distinct source mechanisms and never blur them: **upload from this browser**
  (appropriate for small config files) and **pick a file already on the device** via the
  allowlisted browse API in §5.7 (the correct path for a multi-gigabyte `.bak`). Do not present a
  free-text host-path box, which would be both unsafe and a lie about what the browser can do.
- Use the same browse-handle picker for the backup **destination**.
- **Replace the lost Explorer affordance.** The desktop app opened the output folder for the
  technician after a backup (`BackupService.cs:276`); a server must not do that. The result step
  must instead show the resolved destination path in mono type with a copy-to-clipboard control,
  plus a direct artifact download. Losing this silently would be a real parity regression.
- Provide checksums and file size for completed artifacts.

#### Maintenance

- Do not place destructive buttons beside routine backup controls.
- Show the exact services, paths, tables, and branch affected.
- Require typed confirmation.
- Never use a toast as the only record of success or failure.

#### Activity

- Use mono type for timestamps, IDs, paths, and structured details.
- Allow filtering by status, type, date, and requester.
- Provide copy correlation ID and export sanitized diagnostic bundle.

### 8.8 Content style

- Use active labels: “Save settings,” “Test database,” “Start service,” “Create backup.”
- Preserve the action name through confirmation and completion.
- Explain a failure and the next corrective action.
- Show “Last checked 14:32:08” instead of an ambiguous “Online.”
- Use sentence case. Avoid all-caps body copy.

### 8.9 Accessibility and localization readiness

- Meet WCAG 2.2 AA for contrast, keyboard operation, focus visibility, names, roles, error association, and live progress.
- Use `aria-live` conservatively for operation stage changes, not every log line.
- Move focus deliberately when dialogs open/close and after route-level errors.
- Never trap focus in non-modal activity panels.
- Support 200% zoom and widths down to 360 px.
- Use CSS logical properties and extract user-facing strings so later Arabic/RTL support does not require structural rework.

## 9. Delivery sequence

Phases map one-to-one onto the sessions in `NET10_ANGULAR22_SESSION_PROMPTS.md`, so there is a
single authority for scope. That document holds the tasks, tests, and verification commands; this
section holds the sequence, the exit criterion, and the gate.

Every session ends with: tests added alongside the implementation, verification commands run and
their real output reported, `docs/migration/SESSION_LOG.md` updated, and one commit on branch
`migration/session-NN`. WinUI is not removed before Session 14.

| # | Status | Session | Exit criterion |
| --- | --- | --- | --- |
| 00 | Complete | Baseline, parity matrix, screen map, ADRs | Every current feature and command appears in the parity matrix with a target route, target API, safety class, and test level. All §13 decisions are recorded as answered. |
| 01 | Complete | Deterministic toolchain and skeleton | Clean checkout restores from pinned versions with zero wildcards; Agent serves an empty Angular production build; WinUI still publishes and runs. |
| 02 | Complete | Contracts, API conventions, auth, file browse | Contracts serialize with intended casing and UTC; no secret or raw path can appear in any contract; generated Angular client compiles under strict TypeScript; browse API rejects every escape attempt. |
| 03 | Complete | Secure configuration | Fresh defaults contain no credential and no environment-specific address; both secrets round-trip in the service-owned store; non-secret legacy import is idempotent and leaves `config.json` untouched. |
| 04 | Complete | Job engine, SSE, audit log | A fake operation survives browser refresh; SSE drop does not cancel server work and does not duplicate a command on reconnect; conflicting locks serialize or reject per policy. |
| 05 | Complete | **Design system and shell** | Shell works at 360/768/1280/1600 px in light and dark, keyboard-only and with reduced motion; zero runtime requests leave the agent origin; the signal path is the one memorable visual. |
| 06 | Complete | Overview, Device, Settings | Configuration behavior matches WinUI without ever returning a secret; the signal path is backed by real agent evidence; the service account can actually reach SQL Server. |
| 07 | Complete | Windows service management | Start/stop/restart outcomes correct against fakes and one opt-in disposable-service fixture; unauthorized, invalid, conflicting, and timed-out commands all fail safely. |
| — | Planned | **GO / NO-GO GATE** | See below. |
| 08 | Complete | Local backup | Existing selectable components and archive compatibility preserved; cancellation and partial failure explicit; destination chosen by browse handle; Explorer affordance replaced. |
| 09 | Planned | Restore backend and archive hardening | Traversal, absolute paths, ZIP bomb, excessive entries, checksum mismatch, ambiguous `.bak`, and wrong branch all rejected; no restore can begin without a fresh server preview. |
| 10 | Planned | Restore UI flows | All three restore modes work end to end against fakes; confirmation focus management is correct; a stale preview fails closed in the UI as well as the API. |
| 11 | Planned | Cleanup and branch reset safety | Drive roots, Windows, Program Files, ProgramData root, user profile root, install roots, parent traversal, unresolved variables, unapproved UNC, and junction escapes are all rejected; a forged client confirmation cannot execute anything. |
| 12 | Planned | DB Downloader | Existing newest-created-folder and stable-size behavior still covered; branches progress independently; no RDB credential or UNC path reaches the browser; SMB proven under the real service identity. |
| 13 | Planned | UI polish, accessibility, release hardening | Every release gate in §11 is Pass or an explicitly accepted exception; visual snapshots land here, not earlier. |
| 14 | Planned | Installer and cutover | Offline clean install, upgrade, and rollback all evidenced; parity matrix fully run; WinUI removed only after explicit approval, in a dedicated commit. |

### The gate after Session 07

Sessions 00–07 consume roughly a third of the effort and, at their end, configuration and Windows
service control are working in a browser against the real agent. That is the cheapest honest test
of the entire premise. Stop and decide explicitly:

- Does the agent's service identity actually work for SQL Server and Windows service control on a
  representative device?
- Is the Angular UI genuinely better to use than the WinUI pages it replaces? Since UI
  modernization is the whole driver (§0.1), a "no" here invalidates the project regardless of how
  clean the backend is.
- Is the remaining scope still credible against the measured baseline in §0.2?

If any answer is no: **stop, keep WinUI, and write down what blocked it.** Abandoning at one third
is a good outcome compared with discovering the same problem at Session 14.

### Sessions requiring security judgment

Sessions **03** (secrets), **09** (archive hardening), and **11** (destructive path policy) carry
the highest risk of a subtle, exploitable mistake. Use the strongest available reasoning for these
and review them most carefully. Sessions 01 and 13 are the most mechanical.

## 10. Testing strategy

### 10.1 .NET tests

- Domain rules, operation state machine, resource locks, confirmation challenges, path policy and redaction.
- Application services for every success, partial success, cancellation, timeout and failure branch.
- Configuration migration and secure-store behavior.
- SQL generation and parameterization without requiring production databases.
- Windows service, SMB, SQL Server and file adapters behind integration fixtures.
- API authentication, authorization, antiforgery, validation, rate limiting, idempotency and Problem Details.
- Audit-file append behavior, including that every destructive operation is recorded and that no record contains a secret.
- Upload/download streaming and archive abuse cases.

### 10.2 Angular tests

- Signal-based feature stores and API adapters.
- Typed form validation and secret “keep/replace/clear” behavior.
- Fresh/stale/offline state rendering.
- Confirmation challenge UI and focus behavior.
- Agent-unreachable and version-incompatible states disable every host mutation.
- Component keyboard and accessible-name tests.

### 10.3 End-to-end journeys

Playwright automation is limited to the five highest-risk journeys. Every one of these runs against
**fake SQL, service, and SMB adapters and temporary sandbox directories only** — never a real RMS
installation, database, service, or share.

1. **Configuration** — import legacy non-secret config, enter both secrets, test the SQL connection, verify the branch, save, reload, and confirm no secret is ever returned.
2. **Service control** — refresh, start, stop, restart, double-click prevention, and SSE drop/reconnect.
3. **Backup** — select components, review, run, refresh the browser mid-progress, then download the artifact.
4. **Restore** — preview and execute each of the three modes, including a stale-preview rejection.
5. **Cleanup** — preview, typed confirmation, execute; plus a forged-confirmation attempt that must fail.

Verified manually with recorded evidence rather than automated:

- Branch reset preview and execution.
- Multi-branch DB Downloader batch with independently ready, timed-out, and failed branches.
- SMB access under the real Windows Service identity (§5.6) — the highest-risk unautomatable check.
- Installer clean install, upgrade, and rollback (Session 14).
- Keyboard-only and screen-reader passes over the shell and every dialog.

## 11. Release gates and acceptance criteria

### Functional

- Every row in the parity matrix passes or carries an explicitly accepted difference.
- No feature requires WinUI.
- A browser refresh during a long job neither loses the job nor duplicates the command.
- Every destructive operation appears in the audit file.

### UI modernization — the driver, so these are release gates, not nice-to-haves

- Every WinUI page in the §8.6.1 map has a working target route.
- The branch signal path is implemented and is the primary visual on the overview.
- No screen resembles a generic admin template; the Session 05 self-critique is recorded.
- All three bundled font families and the icon set render with zero network requests.
- Light and dark themes both pass WCAG 2.2 AA on every semantic combination.
- The UI works at 360 px, 768 px, and common desktop widths, at 200% zoom.
- Status is never conveyed by color alone.
- No operation outcome exists only in a transient toast.
- Reduced motion is respected and status stays comprehensible with all motion removed.
- Keyboard-only operation and a screen-reader smoke pass both succeed.

### Deployment

- Fresh install completes with network adapters disconnected.
- The installed application performs every local operation without internet.
- All runtime fonts, icons, scripts, and styles are served locally.
- Uninstall explicitly offers to retain or remove configuration, audit, and backup data.

### Security

- No hard-coded credential and no environment-specific address anywhere in the tree.
- Both secrets are encrypted at rest and never returned by any endpoint.
- Every mutation endpoint requires authentication, authorization, and antiforgery.
- Destructive operations require a current server challenge and typed confirmation, with policy recomputed at execute time.
- Path policy prevents traversal, reparse-point escape, and protected-root deletion — proven by tests, including the browse API.
- A security test asserts no non-loopback listener exists.
- A secret-scan test asserts no test secret appears in any response, log line, or audit record.

### Engineering

- .NET and Angular production builds are warning-clean under the agreed policy.
- Unit, agent integration, Angular, and Playwright suites pass.
- Exact dependency versions and lockfiles are committed; zero wildcards remain.
- Installer and rollback are tested on a clean Windows machine.
- Third-party license notices are included for the bundled fonts and icons.

## 12. Major risks and mitigations

| Risk | Severity | Mitigation |
| --- | --- | --- |
| **The service account cannot do what the elevated technician could** — SQL login, SMB mapping, or file ACLs fail under the service identity | **Highest** | Decide identity in a Session 00 ADR; prove SQL in Session 06 and SMB in Session 12 on a representative device, before UI is built on top. `WNetAddConnection2` in session 0 is the specific trap (§3.4 item 14). |
| Cleanup causes catastrophic deletion | **Highest** | Managed roots, canonical path policy after resolution, protected-root denylist, reparse-point rejection, preview + expiring challenge + typed confirmation, policy recomputed at execute, and integration tests for every rejection case. |
| **The new UI is not actually better**, so the project fails at its own driver | High | The Session 07 gate exists for exactly this. Session 05 carries a mandatory self-critique. UI quality is a release gate (§11), not a nice-to-have. |
| A web API increases the attack surface | High | Loopback-only bind with no configuration path to change it, same origin, authenticated mutations, antiforgery, strict validation, strict CSP, and a test asserting no non-loopback listener. |
| Technicians cannot select a file that already exists on the device | High | The allowlisted browse API with opaque handles (§5.7), designed in Session 02 rather than discovered in Session 09. |
| Legacy secrets are unrecoverable under the service identity | Medium | Accepted, not mitigated: the two passwords are re-entered once on first run (§5.5). Non-secret config still migrates. |
| Long SQL/file work is interrupted by an agent restart | Medium | Accepted, and no worse than the current application, which loses all job state on exit. Resource locks prevent concurrent conflict; the UI states plainly that a restart clears in-flight jobs. |
| Browser loses connection mid-command | Medium | The agent owns job lifetime. The UI rehydrates read-only through the operation ID and never re-issues a command as recovery. |
| Scope creeps back toward the cut features | Medium | §0.3 records each cut with its justification. Re-adding one is a decision with an owner, not a convenience during a session. |
| Angular/.NET dependencies cannot restore at a branch | Low | The runtime package is fully self-contained; builds happen centrally from pinned lockfiles. No branch device ever needs Node or NuGet. |
| WinUI is removed too early | Low | Strangler migration, parity matrix, dedicated cutover approval, and a Session 14 boundary. Note that WinUI must be **published and run** to verify the baseline, not merely built (see `run_app.cmd`). |

## 13. Decisions — already made

These were open assumptions in the first draft. They are now **decided**, and Session 00 records
them as ADRs rather than inventing them. An implementing agent must not re-open or silently
reinterpret a decision here; if one appears wrong, stop and raise it.

| # | Decision | Status |
| --- | --- | --- |
| 1 | Windows 10/11 **x64** is the only agent platform. No `win-arm64`. | Decided |
| 2 | No public internet exposure, no cloud service, no central server. | Decided |
| 3 | **Local loopback only.** LAN/remote is not in v1 and needs no forward-compatibility scaffolding. | Decided (§0.3) |
| 4 | Existing backup ZIPs remain readable. Legacy `config.json` is imported for **non-secret values only**; the two secrets are re-entered once. | Decided (§5.5) |
| 5 | English ships first. Use CSS logical properties and extract user-facing strings so Arabic/RTL needs no structural rework — but do not build a localization layer in v1. | Decided |
| 6 | The installer creates a Windows Service and requires administrator approval. | Decided |
| 7 | **No SQLite.** In-memory job registry plus an append-only JSONL audit file for destructive operations. | Decided (§0.3) |
| 8 | WinUI remains until parity sign-off and is removed in a dedicated Session 14 commit. | Decided |
| 9 | **No SignalR.** SSE for progress, REST for state. | Decided (§0.3) |
| 10 | **No PWA, service worker, or IndexedDB.** | Decided (§0.3) |
| 11 | Authorization is a single principal: member of the local Administrators group. | Decided (§5.6) |

Two decisions genuinely remain open and must be settled in Session 00:

- **Windows Service identity** — dedicated local service account (preferred) versus LocalSystem.
  This is the highest-risk open item; see §5.6 and the risk table.
- **C# 14 versus staying on C# 13** — the SDK supports 14 and `Directory.Build.props` currently
  pins 13. Choose deliberately and record why.

## 14. Session sequence

Use `NET10_ANGULAR22_SESSION_PROMPTS.md` in this order. The exit criterion for each is in §9.

| # | Status | Session |
| --- | --- | --- |
| 00 | Complete | Baseline, parity matrix, screen map, ADRs |
| 01 | Complete | Deterministic toolchain and solution skeleton |
| 02 | Complete | Contracts, API conventions, auth, and host file browse |
| 03 | Complete | Secure configuration *(security judgment)* |
| 04 | Complete | Job engine, SSE, and audit log |
| 05 | Complete | Angular design system and application shell |
| 06 | Complete | Device overview and configuration |
| 07 | Complete | Windows service management → **GO / NO-GO GATE** |
| 08 | Complete | Local backup |
| 09 | Planned | Restore backend and archive hardening *(security judgment)* |
| 10 | Planned | Restore UI flows |
| 11 | Planned | Cleanup and branch reset safety *(security judgment)* |
| 12 | Planned | DB Downloader |
| 13 | Planned | UI polish, accessibility, and release hardening |
| 14 | Planned | Offline installer and cutover |

Rules:

- One session at a time, in order. Never ask an agent to "continue as far as possible."
- Do not combine destructive-operation sessions (09, 11) with installer or cutover work.
- Review the diff, the verification output, and the session log before starting the next session.
- Stop at the Session 07 gate and make an explicit decision.

### 14.1 What changed from the first draft of this plan

Recorded so the reasoning is not lost and cut scope is not silently reinstated:

- Driver established as UI modernization (§0.1); measured baseline added (§0.2).
- Cut SQLite, SignalR, PWA/service worker/IndexedDB, LAN mode with pairing and roles, legacy secret
  migration, SBOM, soak tests, Storybook, and `win-arm64` — each with a justification in §0.3.
- Added §5.7 host file browse, which closes a gap that would have blocked the restore work.
- Added the `WNetAddConnection2`-under-service-identity risk, which was previously unrecorded.
- Added the WinUI→Angular screen parity map (§8.6.1) and the Explorer-affordance replacement.
- Added the Session 07 go/no-go gate.
- Split the original restore session in two; renumbered accordingly (16 sessions → 15).
- Turned §13 from agent-invented assumptions into recorded decisions with two genuinely open items.
- Annotated every §3.4 finding with verified file and line evidence.

## 15. Official baseline references

- [.NET releases and support](https://learn.microsoft.com/dotnet/core/releases-and-support) — .NET 10 is an LTS release supported through November 2028.
- [What’s new in C# 14](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14) — C# 14 is supported by the .NET 10 SDK.
- [Angular version compatibility](https://angular.dev/reference/versions) — **authoritative source for the Angular 22 Node.js, TypeScript, and RxJS matrix. Read it in Session 01 and pin from it.** An earlier draft of this document asserted TypeScript `>=6.0.0 <6.1.0` from memory; treat that as unverified.
- [Host ASP.NET Core in a Windows Service](https://learn.microsoft.com/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-10.0)
- [ASP.NET Core antiforgery guidance](https://learn.microsoft.com/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
- [Problem Details / RFC 9457 in ASP.NET Core](https://learn.microsoft.com/aspnet/core/web-api/handle-errors?view=aspnetcore-10.0)
- [Windows Data Protection API scoping](https://learn.microsoft.com/dotnet/standard/security/how-to-use-data-protection) — relevant to §5.5 and to why legacy user-scoped ciphertext is unreadable by a service account.
- [WCAG 2.2 AA](https://www.w3.org/TR/WCAG22/) — the §11 accessibility gate.

Local repository references an implementing agent should read before Session 01:

- `run_app.cmd` — shows that WinUI requires `dotnet publish` plus `POS_ADMIN_SKIP_ELEVATION=true` to run. A successful `dotnet build` does **not** prove the parity baseline still works.
- `Directory.Build.props` — current `LangVersion 13.0`, `Nullable enable`, `AnalysisLevel latest`.
- `PosAdminTool.sln` — 4 source and 3 test projects; no `global.json` exists yet.
