# POS -> RMS+ Support Hub Merge Preparation Plan

> **ACTIVE canonical programme document.** This plan supersedes the future-execution direction of
> `docs/NET10_ANGULAR22_MIGRATION_PLAN.md` after completed Session 08. It describes the verified
> repository state at the 2026-08-09 reconciliation and the work required before a possible,
> owner-approved cross-project integration.

> The POS repository remains separate. No repository merge, Angular integration, standalone
> installer cutover, WinUI removal, or POS-M01 implementation is authorized by this document alone.

## 1. Executive decision

Sessions 00-08 of the original .NET 10 + Angular 22 migration are accepted existing architecture
and must be preserved. The POS repository now pivots from standalone-product completion to
merge-readiness for RMS+ Support Hub.

The approved direction is:

1. Preserve useful POS work and keep the POS repository separate temporarily.
2. Freeze further standalone Angular expansion after the existing Session 08 implementation.
3. Continue only POS-owned domain, application, privileged Windows/SQL/SMB, security, portability,
   operation, audit, and merge-readiness work.
4. Let RMS+ Support Hub finish its parallel preparation through its current Session 08.
5. Stop when both repositories are preparation-complete.
6. Review both repositories together.
7. Create the real repository merge and Angular integration programme only after explicit owner
   approval.

The final Angular shell, global navigation, shared components, visual system, branding, themes,
cross-tool UX, and integrated POS route experience belong to RMS+ Support Hub. POS supplies
portable backend behavior, secure contracts, and integration boundaries.

## 2. Completed Sessions 00-08 baseline

The completed milestones remain historical and valid. They are indexed in `.ai/HISTORY.md` and
detailed in `docs/migration/SESSION_LOG.md`.

| Session | Preserved baseline |
| --- | --- |
| 00 | Baseline audit, feature/parity map, risks, and architecture decision records |
| 01 | Deterministic .NET/Angular toolchain, solution skeleton, and Windows Agent/Web foundation |
| 02 | Versioned contracts, API conventions, loopback/auth boundaries, antiforgery, and opaque file browsing |
| 03 | Service-owned configuration, legacy non-secret import, DPAPI secret handling, and redacted contracts |
| 04 | In-memory operation engine, REST state, SSE progress transport, resource locks, and JSONL audit |
| 05 | Branch Signal Desk Angular design system, shell, responsive layout, accessibility foundation, and generated API path |
| 06 | Agent-backed Overview, Device, and Settings workflows |
| 07 | Agent-backed service monitoring/control, authorized commands, audit, and accepted live-SCM evidence gap |
| 08 | Agent-backed local backup workflow, staged manifest/checksum archives, artifacts, recovery, and backup UI |

The recorded Session 08 verification is the current milestone evidence:

| Check | Recorded result |
| --- | --- |
| `dotnet build PosAdminTool.sln -c Release --no-restore` | Passed, 0 warnings / 0 errors |
| `dotnet test PosAdminTool.sln -c Release --no-restore` | Passed, 125 tests across Domain, Application, Infrastructure, and Agent integration projects |
| `npm --prefix src/PosAdminTool.Web run test -- --run` | Passed, 8 tests in 6 files |
| `npm --prefix src/PosAdminTool.Web run e2e -- --grep "backup"` | Passed, 1 backup workflow test using fakes |
| `dotnet publish src/PosAdminTool.WinUI/PosAdminTool.WinUI.csproj -c Release -r win-x64 --self-contained false --no-restore` | Passed |

The old 97/98-test statements are historical Session 05 project-memory facts, not the current
Session 08 result. No real SQL backup was executed.

## 3. Current Domain/Application/Infrastructure architecture

### Domain

`PosAdminTool.Domain` targets `net10.0` and owns portable models, enums, exceptions, and
interfaces. It defines the seams for configuration, SQL/database operations, backup files, backup
API access, SMB repository access, Windows service management, connectivity, cryptography, and
legacy import. It must remain free of ASP.NET, Angular, Windows UI, and host-specific orchestration.

Relevant current models include `AppSettings`, `AgentConfiguration`, downloader settings, backup
jobs, restore file information, operation results, and database resolution rules. Domain contracts
are behavior-neutral and can be reused by a future Support Hub backend composition after a collision
review.

POS-M02 adds the `IRestoreFileSystem` and optional `IDatabaseRestoreVerifier` seams without adding
host-specific orchestration to Domain. `AgentConfiguration.DbFilesPath` remains service-owned and
is not returned through browser configuration DTOs.

### Application

`PosAdminTool.Application` owns use-case policy and orchestration over Domain interfaces:

- `BackupService` is the Session 08 server-adapted path. Filesystem access is behind
  `IBackupFileSystem`, preflight is server-owned, and staging/manifest/checksum behavior is tested
  with fakes.
- `RestoreService` is now the POS-M02 server-owned restore workflow: bounded archive metadata and
  checksum/manifest inspection precedes private temporary extraction, SQL logical-file discovery
  and MOVE planning stay behind testable seams, and configuration overwrites use rollback-capable
  atomic copies. POS-M03 adds the server-owned `MaintenanceService` cleanup/reset policy,
  preview, challenge, recomputation, and explicit partial-outcome workflow; the retained
  `CleanupService` is only a compatibility facade over that boundary. `DbDownloadService` remains
  a legacy capability awaiting POS-M04 hardening; none of these changes authorizes standalone
  Angular UI.
- Configuration, branch verification, import, connection testing, and operation use cases remain
  application-level orchestration rather than browser logic.

### Infrastructure

`PosAdminTool.Infrastructure` targets Windows and owns adapters for:

- SQL Server command execution and backup/restore/reset behavior;
- Windows Service Control Manager and privilege checks;
- service-owned configuration files, atomic writes, legacy import, and machine-scope DPAPI secrets;
- local backup filesystem operations;
- the production restore filesystem adapter and non-destructive SQL restore verification adapter;
- RMS backup-trigger HTTP calls;
- SMB/UNC connections, path resolution, and remote backup repository behavior.

Infrastructure is the privileged boundary. It must not leak credentials, raw exception text,
absolute host paths, or unvalidated targets into browser contracts. Windows-only dependencies remain
isolated here or in the Agent composition root.

## 4. Contracts architecture

`PosAdminTool.Contracts/V1` contains versioned, serializable DTOs grouped by session, device,
configuration, services, operations, backups, restore, maintenance, downloader, files, artifacts,
activity, and common evidence/error types.

The contract rules are:

- Contracts describe capability and evidence, not Domain model internals.
- Secrets are represented by presence/replace/clear semantics, never by returned plaintext.
- Host paths, UNC paths, service names, SQL connection strings, and credential material stay on the
  Agent side. Browser file selection uses an allowlisted browse root and an opaque handle.
- Operation IDs, correlation IDs, artifact IDs, freshness evidence, and typed error codes are the
  integration vocabulary.
- Restore, Maintenance, and Downloader DTOs already exist as architectural seams; their presence
  does not mean their backend endpoints are implemented.
- `/api/v1` remains the current POS API boundary. A Support Hub route prefix or host composition is
  a cross-project decision, not a preparation-session rename.

Generated OpenAPI documents and Angular API clients are derived outputs. They are not hand-edited
merge sources and must be regenerated by the destination build after any approved integration.

## 5. Agent architecture

`PosAdminTool.Agent` is the Windows-only ASP.NET Core composition root. Current registrations and
endpoint groups include:

- loopback Kestrel hosting and static Angular/Web fallback;
- Negotiate authentication and the local-Administrators authorization policy;
- antiforgery token issuance and mutation filtering;
- correlation IDs, RFC 9457 Problem Details, and sanitized errors;
- service-owned configuration and DPAPI secret stores;
- allowlisted file browsing and principal/purpose-bound single-use handles;
- device/session/diagnostic, configuration, service-control, operation, activity, event, backup,
  and maintenance endpoints;
- `OperationRegistry`, `OperationWorker`, `ResourceLockSet`, `OperationAuditWriter`, and
  `ArtifactCatalog`, including bounded maintenance challenges and logical cleanup/reset outcomes;
- service polling and service command workers;
- `BackupService`, `RestoreService`, the physical backup/restore filesystem adapters, bounded
  restore uploads/challenges, and restore endpoint modules.

The Agent owns the request-to-privileged-operation boundary. Angular calls the Agent; Angular never
executes SQL, Windows service, SMB, cleanup, restore, or privileged filesystem operations directly.

POS-M02 maps the secure `/api/v1/restores` upload, preview, and execute backend. POS-M03 now maps
the backend-only `/api/v1/maintenance` cleanup-preview/execute and branch-reset-preview/execute
endpoints through the same authorization, antiforgery, operation, idempotency, lock,
cancellation, audit, and sanitized-error boundaries. Downloader remains deferred to POS-M04; no
integrated frontend is authorized by this backend work.

## 6. Current Angular implementation

`src/PosAdminTool.Web` is an Angular 22 standalone workspace embedded into Agent publish output.
The existing Branch Signal Desk implementation includes:

- implemented Agent-backed Overview, Device, Settings, Services, and Backups screens;
- a shared Agent API service, same-origin mutation/antiforgery handling, generated OpenAPI client,
  status/freshness presentation, dialogs, toasts, and responsive shell components;
- placeholder routes for Restore, Maintenance, Downloads, and Activity;
- unit, accessibility-oriented, and backup end-to-end coverage recorded through Session 08.

This Angular implementation is retained as completed migration work and reference material. It is
not a mandate to fill the placeholder routes as a standalone POS product. New POS sessions must
not add global navigation, shared visual components, broad UI polish, final visual snapshots, or
standalone Restore/Maintenance/Downloader screens. Future POS-specific UI work is acceptance
criteria and integration input for Support Hub.

Generated files under `src/PosAdminTool.Web/openapi/` and
`src/PosAdminTool.Web/src/app/core/api/generated/` remain derived and must not be edited directly.

## 7. Retained WinUI role

`PosAdminTool.WinUI` remains in the solution, buildable, and publish-validated for `win-x64`. It
continues to be the compatibility and parity baseline for configuration, service control,
backup/restore, maintenance, downloader, and activity workflows while the Agent/backend seams are
prepared.

Do not remove WinUI, Windows App SDK dependencies, XAML publish workarounds, or `run_app.cmd`.
Do not perform standalone installer cutover. Removal is deferred until cross-project review and
explicit owner approval, then must be a dedicated, easily reviewable change.

## 8. Privileged-operation architecture

The intended flow is:

```text
Support Hub UI or retained WinUI
        -> versioned POS contract / local Agent boundary
        -> authorized Agent endpoint
        -> application policy and operation registry
        -> resource locks and cancellation token
        -> Infrastructure SQL / SCM / SMB / filesystem adapter
        -> sanitized operation state, artifact metadata, and audit evidence
```

The Agent is same-origin and loopback-only. Long work runs outside the HTTP request lifetime. REST
is state truth; SSE is a progress transport and can be reconnected. Browser refresh recovery is
read-only and must not replay a mutation. Destructive operations must use server-side policy,
preview, one-use challenge, typed confirmation, execute-time recomputation, resource locking,
cancellation, audit, and focused tests.

No SQLite, SignalR, PWA, service worker, IndexedDB, public listener, LAN mode, or remote management
is part of this preparation programme.

## 9. Security model

The retained security boundary is:

| Boundary | Current rule |
| --- | --- |
| Device/runtime | Windows 10/11 x64, per-device, local-only |
| Network | Agent binds to `127.0.0.1:5001`; no configurable LAN/public binding |
| Authentication | Windows Negotiate in the real Kestrel runtime |
| Authorization | Authenticated member of the local Administrators group; no role matrix |
| Browser mutations | Same-origin antiforgery double-submit token |
| File selection | Allowlisted browse roots and opaque, principal/purpose-bound, single-use, expiring handles |
| Secrets | Agent-owned service configuration and machine-scope DPAPI; redacted browser DTOs |
| Errors/logs | Correlation IDs and sanitized messages; no raw sensitive exception text |
| Destructive work | Preview, short-lived one-use challenge, typed confirmation, recomputation, locks, audit |

The representative-device LocalSystem proof for managed roots, Windows service control, and SMB
Session 0 behavior remains an evidence gate. No real RMS database, production endpoint, service, or
SMB share may be used by the preparation sessions.

## 10. Configuration and secrets ownership

POS owns the machine-local configuration schema and secret semantics. The Agent's service-owned
configuration is separate from the retained WinUI profile configuration and is stored under the
service-owned `%ProgramData%\DBS\PosAdminTool` area with restricted ACLs. SQL and RDB secrets are
held separately and protected with machine-scope Windows DPAPI. Legacy import is one-time,
idempotent, read-only, and non-secret; SQL and RDB passwords are re-entered.

RMS+ Support Hub must not become a second secret store or receive raw POS credentials in browser or
shared frontend state. At merge time, the host may own process configuration and logging, but POS
secret interfaces, redaction rules, machine-local paths, and credential isolation must remain
explicitly owned and tested.

## 11. Current operation engine

The current `OperationRegistry` is an in-memory registry with a bounded Channel queue
(`Capacity = 32`), principal-scoped idempotency keys, operation details/summaries, progress events,
cancellation, result artifact IDs, and state transitions. `OperationWorker` holds resource locks,
executes backup work, registers artifacts, writes required audit records, and publishes changes.

POS-M01 closes the queue-versus-retention gap with the injectable `RuntimeRetentionPolicy`. The
default policy retains at most 64 completed operations for one hour, at most 32 events per
operation, at most 64 activity records while preserving active visibility, at most 64 artifacts
for 24 hours, and at most 256 five-minute file handles. The clock boundary is inclusive. Active
queued/running operations and active artifact download leases are never evicted.

The verified POS-M01 correction is:

| State | Verified POS-M01 behavior | Retention consequence |
| --- | --- | --- |
| `_entries` | Completed records are retained by count and inclusive age boundary; active records remain visible | The dictionary is bounded by active queue/worker capacity plus the explicit completed policy |
| `_idempotency` | Principal/key mappings are removed with their evicted operation and stale mappings are pruned | A retained key returns the existing operation; an evicted key safely creates a new operation |
| Completed operation records | Completed entries are available only during the one-hour/64-record retention window | Eviction returns the documented operation not-found result and never removes queued/running work |
| Entry event lists | Required queued/running/terminal events and bounded warning evidence are retained | Progress/evidence storage is bounded to 32 events and messages are sanitized, newline-safe, and path/secret/exception-redacted |
| Activity records | Activity is derived only from bounded operation state | The list is capped at 64 records while all active operations remain visible |
| Artifact catalog | Metadata expires after 24 hours; valid entries are not evicted to make room; full admission fails closed | Active download leases defer expiry deletion until the response stream is disposed; missing/expired artifacts return not-found |
| File-handle store | One-use handles are removed from active use, expired handles are pruned, and valid handles are never evicted for capacity | The five-minute/256-handle policy rejects new issuance when no safe slot exists |
| Cancellation cleanup | Worker tokens link operation and shutdown cancellation; locks release on every acquisition failure; completed entries release work items | Backup temporary, staging, and unpublished post-move archives are cleaned with cancellation-independent cleanup |
| Messages/audit | Operation messages and error codes are bounded/sanitized; audit remains destructive-operation-oriented JSONL | Audit writes use a non-cancelled completion token and never expose raw exception details |

POS-M02 extends the same bounded architecture to restore state: upload slots and staged bytes are
reserved while streams are in flight, uploads are released on rejection/cancellation/expiry/
operation completion, challenges are short-lived and capped, restore work uses `sql`, `services`,
and `filesystem-cleanup` locks, and no separate unbounded restore registry or cache was added.

POS-M03 extends the operation engine to cleanup and branch-reset work. Cleanup uses `services` and
`filesystem-cleanup` locks; branch reset uses `sql` and `services` locks. Each queued work item
retains only the logical mode and preview fingerprint, while the worker reloads service-owned
configuration and the application service recomputes policy immediately before each destructive
seam. Terminal operation details and JSONL audit entries retain logical per-item attempted versus
completed state, residue uncertainty, recovery guidance, and stable failure codes without host
paths, raw SQL, credentials, or exception text.

The ADR-approved in-memory architecture remains; no durable database or SQLite was introduced.

## 12. Backup architecture

Session 08 is the strongest current Agent workflow and the reference pattern for future backend
work:

- `BackupService` delegates host I/O through `IBackupFileSystem`.
- Server-owned preflight validates destination, branch identity, database identifiers, config
  sources, free space, and reparse-point conditions.
- SQL backups are written to a staging directory, with a compatibility retry for the primary
  database path; the final archive is produced only after staged work is described.
- Versioned manifest/checksum archive entries, explicit success/partial/failure/cancellation
  outcomes, and best-effort staging cleanup are present.
- `BackupOperationWorkItem` carries internal settings and a redeemed destination, never a browser
  raw path. The operation registry carries progress, idempotency, cancellation, locks, and artifact
  IDs.
- `ArtifactCatalog` exposes principal-scoped opaque metadata and streamed content with safe display
  names. The browser does not launch Explorer or receive host paths.
- Angular has the select/review/run/progress/result/catalog flow and refresh recovery, using fake
  SQL/filesystem adapters in end-to-end tests.

No real `BACKUP DATABASE` command was authorized or executed. Future restore, cleanup, and download
work must follow this server-owned, fake-testable boundary rather than adapting the old WinUI UI
directly.

## 13. Known risks

| Risk | Current status and preparation response |
| --- | --- |
| Runtime state grows without bound | POS-M01 closed the confirmed gap with injectable operation, event, activity, artifact, and file-handle retention; full Release validation passed 141 .NET tests |
| Restore archive validation is weak | POS-M02 closes the Agent/backend gap with bounded pre-extraction ZIP inspection, manifest/checksum/branch/destination validation, server-derived preview/challenge recomputation, and fake-only tests; no real restore was executed |
| Cleanup/reset safety is client/legacy driven | POS-M03 closes the Agent boundary with canonical managed-root policy, server-derived preview/challenge, execute-time recomputation, locks, and explicit partial outcomes; retained WinUI compatibility calls fail closed when policy is not configured |
| Downloader lacks Agent security/operation boundary | Legacy service has direct endpoint/SMB/credential behavior; POS-M04 |
| LocalSystem managed-root and SMB Session 0 proof | Representative-device evidence remains required; do not guess |
| Manual live-Agent Negotiate/SSE evidence | Not recorded as a current automated replacement for fake integration tests |
| Frontend duplication during merge | Standalone Angular expansion is frozen; Support Hub owns final frontend |
| Premature WinUI removal/cutover | WinUI retained until cross-project review and explicit approval |
| Repository collisions | POS-M05 produces project/file/dependency/ownership map before any merge |

## 14. Known architecture corrections

POS-M01 verified and closed the following corrections while preserving the ADR-approved in-memory
architecture:

1. Operation, idempotency, completed-record, event, activity, artifact, and file-handle retention
   are genuinely bounded without adding durable storage or SQLite.
2. Active queued/running operations and valid artifact downloads are preserved while completed state
   expires through a deterministic, injectable clock/count policy.
3. Cancellation and lock disposal are covered on success, failure, cancellation, worker shutdown,
   lock wait, and backup staging/post-move cleanup paths.
4. Operation/audit messages remain sanitized and stable; no paths, credentials, or raw exception
   details enter browser-facing operation evidence.
5. Stale Session 05 claims remain historical; the recorded Session 08 baseline is 125 .NET tests,
   8 Angular tests in 6 files, the backup E2E gate, and WinUI publish, while POS-M01 full Release
   validation passed 141 .NET tests and retained WinUI publish.
6. Keep the historical pre-Agent `docs/migration/CURRENT_STATE.md` and old migration runbooks
   clearly labeled as historical, not current execution authority.
7. Treat existing Restore/Cleanup/Downloader DTOs and legacy application services as seams to
   harden, not as permission to expose unsafe standalone UI.

POS-M02 verified and closed the restore backend preparation gate. The retained backend now keeps
browser uploads separate from principal/purpose-bound device browse handles; validates bounded ZIP
metadata, hostile paths, reparse/symlink indicators, allowed content, manifest/checksum/branch
evidence, SQL logical files, destinations, and free space before private extraction; and redoes the
complete policy before challenge redemption and queued execution. Focused Release coverage passed
17 Application restore/planning tests and 13 Agent restore/upload/challenge tests; the complete
Release solution passed 170 .NET tests with zero failures, and the retained WinUI `win-x64` publish
gate passed. Tests used disposable fakes and temporary directories only; no real database restore,
RMS configuration overwrite, or Windows service stop was executed.

POS-M02R is the corrective architecture checkpoint for the POS-M02 destructive-outcome review. It
removes guessed SQL logical-file names and fails closed when server inspection cannot produce a
usable MOVE plan. Restore execution now records destructive milestones and distinguishes clean,
cancelled, failed, and partial/recovery-required outcomes for configuration rollback, service
restart, verification, and cancellation boundaries. Partial restore failure codes are preserved
through Agent operation details and sanitized destructive audit records; focused correction tests
use disposable fake database/service/filesystem adapters and temporary directories only. The complete
Release solution passed 178 .NET tests and the retained WinUI `win-x64` publish gate passed.

POS-M02R2 closes the verified late-cancellation terminal-result race. Once `RestoreService` returns
its finalized `OperationStatus`, `OperationWorker` maps it directly without reinterpreting a later
cancellation signal, and Restore completion preserves finalized success, partial, and failure
outcomes through the existing entry cancellation guard. Focused Release coverage passed 44
Application tests and 106 Agent integration tests; complete Release solution validation passed 182
.NET tests and the retained WinUI `win-x64` publish gate passed. The correction continues to use
fake adapters and temporary directories only.

POS-M02R3 - Interrupted SQL Restore Truth & Final Restore Safety Closure closes the remaining
destructive-truth gaps identified by the early Claude Opus 5 architecture/security review. Restore
now records `DatabaseRestoreAttempted` immediately before the destructive SQL seam and reports an
interrupted attempt as `PartialSuccess` with `restore.database_restore_interrupted` and explicit
database-verification recovery guidance; ordinary cancellation remains available only before SQL
invocation. The fake SQL seam records attempted versus completed invocation, and focused tests cover
pre-invocation cancellation, interrupted cancellation, interrupted exception, successful completion,
full-restore configuration gating, and real `OperationWorker` Restore wiring. Restore audit records
now carry sanitized logical mode (`full`, `database-only`, or `config-only`) and target database
identity. Bare `.bak` sources require exactly one positive matching branch token; conflicting,
missing, or ambiguous evidence fails closed, while ZIP manifest validation remains unchanged. The
dead `ContainsBranchToken` helper was removed. Focused Release coverage passed 23 Application
Restore tests and 24 Agent Restore/worker/audit tests; complete Release solution validation passed
49 Application, 109 Agent integration, 25 Infrastructure, and 7 Domain tests (190 total), and the
retained WinUI `win-x64` publish gate passed. All validation used fakes and temporary directories;
no real SQL restore, RMS configuration overwrite, Windows service operation, or device-state
mutation was executed. Findings addressed: HIGH-1, MEDIUM-1, MEDIUM-2, MEDIUM-3, and applicable
LOW-1. The early Claude Opus 5 Restore follow-up was subsequently cleared by explicit owner
authorization for POS-M03; POS-M03 did not broaden or redesign Restore.

POS-M03 - Cleanup & Branch Reset Backend Safety is complete. The Agent now owns canonical managed
path policy (containment, protected/install/data roots, environment expansion, drive-relative and
UNC rules, reparse/junction/symlink inspection, and root-target rejection), server-derived cleanup
and branch-reset previews, fresh principal-bound one-use challenges, typed confirmation,
principal-scoped idempotency, conflicting resource locks, execute-time recomputation, and
sanitized logical operation/audit evidence. Cleanup and SQL reset stages record per-item attempted
versus completed truth and preserve residue/recovery guidance for partial or interrupted seams;
service control, filesystem, and SQL execution remain behind injectable interfaces. Focused Release
coverage passed 9 Application maintenance tests and 11 Agent maintenance/worker tests. Complete
Release validation passed 210 .NET tests, the solution build passed with zero warnings/errors, and
the retained WinUI `win-x64` publish passed. All POS-M03 validation used disposable fakes or
temporary test infrastructure; no real file cleanup, database reset, Windows service control, or
device-state mutation ran, and no Angular Maintenance UI was added.

## 15. RMS+ Support Hub ownership boundary

| Responsibility | POS repository owns | RMS+ Support Hub owns |
| --- | --- | --- |
| Domain and use cases | POS domain rules, application workflows, validation, operation semantics | Consumes stable capability contracts |
| Privileged execution | SQL Server, Windows SCM, SMB, filesystem, cleanup/reset, restore, downloader backend | Does not perform privileged work in browser |
| Agent/security | Loopback host behavior, auth contract, antiforgery, authorization, secret isolation, audit, operation state | Integrates with approved local API boundary |
| Configuration | Machine-local schema, secret handling, redaction, migration | Host-level composition only after explicit boundary decision |
| Contracts | POS versioned DTOs, error codes, operation/artifact semantics | Final integrated route/API composition after review |
| Frontend shell | Existing Angular is retained as reference and Session 08 evidence | Final shell, global navigation, shared components, forms/tables/cards, branding, design system, themes, motion |
| POS routes | Backend capability and future acceptance criteria | Final integrated POS routes and cross-tool UX |
| WinUI | Retained compatibility/parity baseline | No removal decision before cross-project review |

## 16. Reuse matrix

The following is the preparation default. It is not authorization to move files now.

| Source area | Default disposition | Rationale / merge condition |
| --- | --- | --- |
| Domain models, enums, interfaces | KEEP AS-IS, then namespace audit | Portable and central to POS behavior; preserve API semantics |
| Application policies and services | KEEP WITH RENAME or ADAPT DURING MERGE | Reuse policy, but remove direct unsafe I/O and align host abstractions |
| Infrastructure SQL/SCM/SMB/filesystem/configuration | MOVE DURING MERGE or ADAPT DURING MERGE | Keep privileged adapters isolated under the Support Hub backend boundary |
| Contracts/V1 | KEEP WITH RENAME or ADAPT DURING MERGE | Preserve versioning/redaction; reconcile host route and shared error conventions |
| Agent authorization, operations, audit, artifacts, files | ADAPT DURING MERGE | POS security boundary is valuable but host composition and shared middleware may collide |
| Agent endpoint modules | ADAPT DURING MERGE | Bring capability routes into the approved Support Hub host without copying `Program.cs` wholesale |
| Existing Angular shell and design system | REFERENCE ONLY / DO NOT COPY - SUPPORT HUB ALREADY OWNS IT | Retain as Session 00-08 evidence, not a second global frontend |
| Angular POS feature behavior and API mapping | REFERENCE ONLY, then ADAPT DURING MERGE | Reuse domain-specific behavior after Support Hub route/design decisions |
| Generated OpenAPI document/client | REFERENCE ONLY; REGENERATE IN DESTINATION | Derived output must follow destination host contracts |
| Retained WinUI | KEEP AS-IS until approval; REFERENCE ONLY for merge | Compatibility/parity baseline; no premature removal |
| Backend tests and fake adapters | MOVE DURING MERGE / ADAPT DURING MERGE | Preserve safety evidence, adjust namespace/host fixture ownership |
| Angular tests/snapshots | REFERENCE ONLY, then ADAPT DURING MERGE | Support Hub owns final shell and integrated UX test strategy |
| Build/publish scripts and lockfiles | ADAPT DURING MERGE | Windows Agent/OpenAPI/WinUI requirements must not be silently dropped |
| ADRs and migration evidence | KEEP AS-IS / REFERENCE ONLY | Preserve rationale; new programme docs are the active authority |

## 17. Future responsibility matrix

| Area to settle before merge | POS default owner | Support Hub default owner | Cross-project decision needed |
| --- | --- | --- | --- |
| POS backend assembly | POS module | Host composition | Yes: shared host versus POS Agent process |
| API route prefix/version | POS `/api/v1` semantics | Integrated API gateway/host conventions | Yes |
| DI registration | POS adapters/use cases | Host-wide service lifetime and middleware | Yes |
| Configuration | POS machine-local config and secrets | Host configuration envelope | Yes: no duplicate secret store |
| Logging | POS sanitized operation/audit semantics | Shared log sink/retention/correlation plumbing | Yes |
| Agent identity and privilege | POS local privileged boundary | Host lifecycle/install orchestration | Yes |
| Angular ownership | POS capability data and acceptance criteria | Entire integrated frontend | No for final shell; yes for POS route placement |
| Test ownership | POS unit/security/adapter tests | Shared host and integrated UX tests | Yes |
| Resources/artifacts | POS managed roots, artifact IDs, audit files | Host data-directory policy | Yes: collision-free paths and ACLs |
| Scripts/build | POS Windows/OpenAPI/WinUI requirements | Monorepo orchestration and release pipeline | Yes |

## 18. Monorepo landing map

### Project-level map

The destination names below are candidate landing areas only. Preserve current `PosAdminTool.*`
namespaces until the cross-project review chooses a final namespace and solution layout.

| Current project/area | Candidate Support Hub landing | Disposition | Owner |
| --- | --- | --- | --- |
| `src/PosAdminTool.Domain` | `src/Pos/Domain` or `src/RmsSupportHub.Pos/Domain` | KEEP WITH RENAME only after collision audit | POS |
| `src/PosAdminTool.Application` | `src/Pos/Application` | MOVE/ADAPT; keep use-case boundaries | POS |
| `src/PosAdminTool.Infrastructure` | `src/Pos/Infrastructure` behind host interfaces | MOVE/ADAPT; retain Windows isolation | POS |
| `src/PosAdminTool.Contracts` | `src/Pos/Contracts` or shared API contract area | ADAPT after API/version review | Shared decision |
| `src/PosAdminTool.Agent` | POS backend module or separate local host under Hub solution | ADAPT; do not copy composition root blindly | Shared decision |
| `src/PosAdminTool.WinUI` | Retained POS compatibility project | KEEP AS-IS until cutover approval | POS |
| `src/PosAdminTool.Web` | Existing Hub frontend with POS feature area | DO NOT COPY shell; adapt POS-specific pieces later | Support Hub |
| `tests/PosAdminTool.*` | POS backend test areas plus shared integration fixtures | MOVE/ADAPT | Shared decision |
| `docs/` and `.ai/` | Monorepo POS programme/docs area | REFERENCE/MERGE selectively | Shared decision |

### File-level map

| POS source area | Landing action | Collision or preservation note |
| --- | --- | --- |
| `Agent/Program.cs` | ADAPT into the approved host composition | Authentication, static files, OpenAPI, middleware, and DI may already exist in Support Hub |
| `Agent/Endpoints/*.cs` | MOVE/ADAPT as POS endpoint modules | Preserve route contracts, auth filters, and Problem Details; reconcile route prefix |
| `Agent/Operations/*.cs` | KEEP/ADAPT in POS operation module | Operation registry, worker, locks, work items, and retention must have one owner |
| `Agent/Artifacts`, `Agent/Audit`, `Agent/Files` | KEEP/ADAPT | Artifact paths, JSONL audit, browse roots, and handle stores need one resource policy |
| `Agent/Authorization`, `Antiforgery`, `Correlation` | ADAPT | Reuse security semantics; do not duplicate host middleware or weaken auth |
| `Contracts/V1/**` | MOVE/ADAPT | Preserve DTO redaction, error codes, operation IDs, and version strategy |
| `Domain/Interfaces/**` | KEEP/MOVE with POS module | These are the primary portability seams |
| `Application/Services/BackupService.cs` | KEEP/ADAPT | Session 08 reference implementation and fake-test boundary |
| `Application/Services/RestoreService.cs` and `Application/Restore/**` | KEEP/ADAPT after POS-M02 | Server-owned archive policy, SQL MOVE planning, config rollback, and fake-test seams are now available; reconcile host composition during merge |
| `Application/Services/CleanupService.cs` | KEEP/ADAPT after POS-M03 | Compatibility facade now delegates to the server-owned maintenance policy; Agent endpoints do not accept client paths |
| `Application/Services/DbDownloadService.cs` | ADAPT only after POS-M04 | Preserve behavior while moving credentials/SMB behind Agent policy |
| `Infrastructure/Windows/**`, `Smb/**`, `Backups/**`, `Configuration/**`, `Http/**` | MOVE under POS privileged backend | Windows targeting, service identity, DPAPI, and package collisions need explicit ownership |
| `Web/src/app/app.*` and shell/shared UI | DO NOT COPY | RMS+ Support Hub owns final shell/design system |
| `Web/src/app/core/agent-api.service.ts` | REFERENCE/ADAPT | Integrate with Hub HTTP/auth/error conventions |
| `Web/src/app/features/**` | REFERENCE/ADAPT selectively | Backups/Services/Settings behavior may inform POS route integration; do not copy global shell |
| `Web/openapi/**`, `Web/src/app/core/api/generated/**` | REGENERATE | Generated output follows the destination Agent/API document |
| `WinUI/**`, `run_app.cmd` | KEEP until approved cutover | Do not delete or replace during preparation |
| `tests/**` | MOVE/ADAPT by ownership | Keep fake SQL, filesystem, SCM, SMB, Agent security, and contract tests with their owner |

### Namespace strategy

Keep the `PosAdminTool.*` namespace and `/api/v1` semantics during POS-M01 through POS-M06. Do not
perform a mass rename while the projects are separate. At cross-project review, compare existing
Support Hub namespaces and select one of:

- retain a clearly isolated `PosAdminTool`/`RmsSupportHub.Pos` namespace for POS modules;
- rename to the Support Hub convention with a compatibility mapping for public contracts; or
- keep the POS Agent as a separately hosted local process and integrate by API only.

The final choice is a cross-project decision, not a preparation-session assumption.

### Dependency ownership and collision map

| Dependency/resource | Current POS owner | Merge risk / action |
| --- | --- | --- |
| `Microsoft.Data.SqlClient` | POS Infrastructure | Keep one backend version; audit Support Hub SQL dependencies |
| `System.ServiceProcess.ServiceController` | POS Infrastructure | Keep in Windows-only module; no browser/shared frontend dependency |
| `System.Security.Cryptography.ProtectedData` | POS Infrastructure | Keep with POS secret store; do not replace with a frontend secret mechanism |
| ASP.NET Negotiate/OpenAPI packages | POS Agent host | Reconcile with Support Hub host middleware and exact versions |
| `Microsoft.Extensions.*` | Application/Infrastructure/Agent/WinUI | Centralize only after compatible version audit; retain committed lockfiles |
| Angular 22/RxJS/fonts/icons | POS Web today | Support Hub owns final versions/design assets; do not duplicate package ownership |
| Windows App SDK/CommunityToolkit | WinUI only | Retain until approved cutover; do not pull into Hub backend |
| OpenAPI generated output | POS Web build | Destination-generated; no source ownership collision |
| DI | POS Agent currently registers POS services | Reconcile service lifetime and composition-root ownership |
| Logging/audit | POS Agent operation/audit semantics | Shared sink may be Hub-owned, but POS destructive audit invariants remain required |
| Configuration/secrets | POS machine-local service store | One owner and one ACL/DPAPI policy; no copied secrets |
| Agent ownership | POS Agent currently owns privileged local host | Decide shared Hub host versus separate POS local Agent |
| Angular ownership | POS Web contains shell and screens | DO NOT COPY shell; Support Hub owns final UI |
| Test ownership | POS has four xUnit projects plus Web tests | Preserve safety tests; avoid duplicate integration harnesses |
| Resources/build | POS has Windows publish and Angular/OpenAPI targets | Merge scripts deliberately; preserve WinUI publish and no-Node target-device invariant |
| `.gitignore` | Root .NET ignores plus Web-specific generated/output ignores | Consolidate without unignoring `openapi`, `generated`, `dist`, test artifacts, or secrets |
| Documentation | POS ADRs, migration evidence, active preparation docs | Keep ADR/history; only the two preparation docs are active programme authority |

## 19. Merge collision risks

Before any repository merge, explicitly audit:

1. Duplicate ASP.NET composition roots, authentication schemes, antiforgery configuration, ports,
   static-file fallback, and OpenAPI generation.
2. Duplicate DI registrations and incompatible service lifetimes for operation state, artifacts,
   audit, configuration, `HttpClient`, SQL, SCM, and SMB adapters.
3. Duplicate route prefixes, error-code namespaces, correlation middleware, and generated API models.
4. Namespace/project-name collisions between POS and Support Hub domain, contracts, and services.
5. Package/version collisions across `Microsoft.Extensions`, ASP.NET, OpenAPI, Angular, TypeScript,
   RxJS, fonts, icons, and build tools.
6. Configuration keys, `%ProgramData%` directories, audit/artifact locations, ACLs, service names,
   ports, and Windows Service installation ownership.
7. `.gitignore` behavior for generated OpenAPI clients, Angular `dist`, `node_modules`, test output,
   screenshots, `bin/obj`, publish output, and any local secrets.
8. Test fixture ownership, fake adapters, parallel test isolation, and representative-device gates.
9. Resource naming and frontend route collisions; Support Hub's shell/navigation/design system is
   authoritative.
10. Documentation authority: old migration files remain history, while the two POS preparation
    files and the Support Hub programme must not diverge.

## 20. Deferred standalone work

| Old direction | Disposition |
| --- | --- |
| Session 09 restore backend/archive hardening | COMPLETE as POS-M02; keep backend/security requirements and no standalone Restore UI |
| Session 10 Restore UI | DEFER; preserve behavioral requirements as Support Hub integration acceptance criteria |
| Session 11 cleanup/reset | SPLIT: backend path policy, preview, challenge, locks, audit, and tests are complete as POS-M03; standalone Maintenance UI deferred |
| Session 12 DB Downloader | SPLIT: Agent/backend, SMB, SSRF, credential, cancellation, artifact, and identity work becomes POS-M04; standalone Downloader UI deferred |
| Session 13 UI polish/accessibility/release | DO NOT EXECUTE as written; retain backend/security/reliability criteria, defer broad standalone frontend closure |
| Session 14 installer/cutover | DEFER completely; retain WinUI, do not remove Windows App SDK, do not finalize standalone installer or production cutover |

## 21. Definition of Merge-Ready Candidate

POS may be called a **merge-ready candidate** only when POS-M01 through POS-M05 are complete,
Opus R1 has reviewed the result, and POS-M06 passes its review-gated audit. The candidate must
demonstrate:

- portable Domain/Application boundaries and isolated privileged Infrastructure;
- loopback Agent authentication/authorization, redaction, secret isolation, and contract stability;
- bounded operation, idempotency, event, activity, and artifact state with focused tests;
- hardened restore backend, cleanup/reset backend, and DB Downloader backend using fakes only;
- explicit configuration, logging, audit, resource, dependency, test, and namespace ownership;
- repository cleanliness and a file-level landing map with no duplicate standalone Angular plan;
- retained WinUI and successful required build/publish validation;
- consistent current memory and documentation with no stale Session 09-14 authorization.

The candidate is not a repository merge, a production deployment, a final installer, or a completed
Support Hub frontend integration.

## 22. Preparation programme status

| Item | Status | Gate / outcome |
| --- | --- | --- |
| POS-M01 | Complete | Runtime boundedness, cleanup, artifact lifecycle, focused tests, full Release validation, and retained WinUI publish passed |
| POS-M02 | Complete | Backend restore/archive hardening only; focused restore tests, full Release solution gates, and retained WinUI publish passed; no real restore/config/service operation was executed |
| POS-M02R | Complete | Corrective Restore outcome semantics; fail-closed SQL inspection, explicit partial/cancellation/rollback/restart outcomes, stable Agent/audit failure evidence, focused fake-only tests, 178 Release .NET tests, and retained WinUI publish passed |
| POS-M02R2 | Complete | Restore terminal-result race closed; finalized service outcomes map directly through the Agent and remain stable under late cancellation, with focused 44 Application and 106 Agent tests, 182 Release .NET tests, and retained WinUI publish passed |
| POS-M02R3 | Complete; early Opus Restore follow-up passed | Interrupted SQL destructive truth, recovery-required partial semantics, worker-level Restore wiring, sanitized mode/target audit evidence, and positive bare-BAK branch evidence; focused 23 Application Restore and 24 Agent Restore/worker/audit tests, 190 Release .NET tests, and retained WinUI publish passed |
| POS-M03 | Complete; owner-authorized after the early Opus Restore follow-up gate passed | Cleanup/reset backend safety only: canonical managed-path policy, server-derived preview/challenge, recomputation, locks, partial/residue truth, sanitized audit, and fake-only worker-path coverage; focused 9 Application and 11 Agent maintenance tests, 210 Release .NET tests, zero-warning solution build, and retained WinUI publish passed |
| POS-M04 | Pending POS-M03 and owner authorization | Downloader backend/SMB portability only; not executed in POS-M03 |
| POS-M05 | Pending POS-M02 through POS-M04 | Complete landing/collision audit; then `CLAUDE OPUS 5 REVIEW REQUIRED` |
| R1 | Scheduled after POS-M05 | Claude Opus 5 review gate |
| POS-M06 | Review-gated and owner-authorized only | Final candidate audit |
| R2 | Scheduled after POS-M06 | Claude Opus 5 review before integration |
| Repository merge | Not authorized | Wait for cross-project review and explicit owner approval |

At successful POS-M06 completion, stop with:

```text
POS PREPARATION:
COMPLETE

POS STATUS:
MERGE-READY CANDIDATE

STANDALONE ANGULAR EXPANSION:
FROZEN

WINUI:
RETAINED UNTIL CROSS-PROJECT DECISION

REPOSITORY MERGE:
NOT AUTHORIZED

NEXT:
WAIT FOR RMS+ SUPPORT HUB SESSION 08 AND CROSS-PROJECT REVIEW
```
