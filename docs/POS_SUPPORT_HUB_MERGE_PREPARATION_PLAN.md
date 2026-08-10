# POS -> RMS+ Support Hub Merge Preparation Plan

> **ACTIVE canonical programme document.** This plan supersedes the future-execution direction of
> `docs/NET10_ANGULAR22_MIGRATION_PLAN.md` after completed Session 08. It describes the verified
> repository state at the 2026-08-10 reconciliation and the work required before a possible,
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
  `CleanupService` is only a compatibility facade over that boundary. POS-M04 now hardens
  `DbDownloadService` as a reusable Agent operation path; none of these changes authorizes
  standalone Angular UI.
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
  maintenance, and downloader endpoints;
- `OperationRegistry`, `OperationWorker`, `ResourceLockSet`, `OperationAuditWriter`, and
  `ArtifactCatalog`, including bounded maintenance challenges, logical cleanup/reset outcomes, and
  principal-scoped downloader artifact capabilities;
- the server-owned downloader trigger policy, manual redirects, connection-bound DNS/SSRF
  validation, SMB repository, scoped connection ownership, application-owned trigger milestones,
  and sanitized per-branch operation outcomes;
- service polling and service command workers;
- `BackupService`, `RestoreService`, the physical backup/restore filesystem adapters, bounded
  restore uploads/challenges, and restore endpoint modules.

The Agent owns the request-to-privileged-operation boundary. Angular calls the Agent; Angular never
executes SQL, Windows service, SMB, cleanup, restore, or privileged filesystem operations directly.

POS-M02 maps the secure `/api/v1/restores` upload, preview, and execute backend. POS-M03 maps the
backend-only `/api/v1/maintenance` cleanup-preview/execute and branch-reset-preview/execute
endpoints through the same authorization, antiforgery, operation, idempotency, lock,
cancellation, audit, and sanitized-error boundaries. POS-M04 maps the backend-only
`/api/v1/downloads/batches` trigger boundary through the same operation, idempotency, lock,
cancellation, artifact, and sanitized-error rules; no integrated frontend is authorized by this
backend work.

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
paths, raw SQL, credentials, or exception text. POS-M03R closes the corrective safety gaps by
requiring non-empty valid managed/data/protected/install roots, rejecting symmetric containment
overlap for protected/install roots and allowed reparse destinations, and constraining branch reset
to the server-resolved database, exact-target verification, and the code-owned historical table
allowlist.

POS-M04 extends the operation engine to downloader work. Downloader entries use the `downloader`
resource lock, principal-scoped idempotency, bounded per-branch state/progress, cancellation, and
required sanitized audit. The queued work item contains only a server-owned non-secret
configuration snapshot and validated logical branch codes; the RDB password is loaded from the
Agent secret store at execution time. Completed archives are registered only through the existing
principal-scoped `ArtifactCatalog`, and operation/audit evidence contains no SMB paths, local
staging paths, credentials, or raw exception text.

POS-M04R closes the two verified downloader correctness gaps without changing the approved
architecture. The production trigger transport uses a direct `SocketsHttpHandler.ConnectCallback`
that resolves and validates the actual connection target, normalizes IPv4-mapped IPv6, and opens
the socket only to an approved address while retaining the logical hostname for HTTP/TLS semantics.
The application returns an explicit `DownloaderExecutionResult` with `NotAttempted`, `Failed`, or
`Accepted` trigger state; repository adapters translate SMB/path/I/O failures to stable Domain
codes, and the real Agent worker preserves accepted-trigger truth through repository failure,
partial artifact publication, and cancellation. Focused transport and worker tests use fakes only.

POS-M04R2 closes the remaining remote-trigger lifecycle truth gap. The trigger seam now marks the
dispatch boundary explicitly: pre-dispatch validation, cancellation, and connection-bound SSRF
rejection remain `NotAttempted`; only a definitive rejected response is `Failed`; positive API
acknowledgement is `Accepted`; and cancellation, timeout, connection loss, response transport
failure, or local response-policy failure after dispatch is `OutcomeUnknown`. The browser contract
and sanitized audit carry the explicit trigger state plus safe guidance, while `TriggerAccepted`
remains only a derived compatibility projection. Unknown trigger outcomes terminate safely before
SMB discovery, publish no artifact, do not retry automatically, and use the stable
`downloader.trigger_outcome_unknown` code. Local principal-scoped operation idempotency does not
provide remote API idempotency; no verified remote job-status, reconciliation, or trigger
idempotency contract is available in this repository.

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
directly. POS-M04 preserves the reusable downloader discovery/matching/stability behavior while
adding server-owned trigger SSRF policy, manual redirect validation, DNS address checks, canonical
SMB roots, scoped connection ownership, partial-file cleanup, and opaque artifact publication.
No real RMS endpoint, SMB share, or LocalSystem/Session 0 proof ran in POS-M04 or POS-M04R.

## 13. Known risks

| Risk | Current status and preparation response |
| --- | --- |
| Runtime state grows without bound | POS-M01 closed the confirmed gap with injectable operation, event, activity, artifact, and file-handle retention; full Release validation passed 141 .NET tests |
| Restore archive validation is weak | POS-M02 closes the Agent/backend gap with bounded pre-extraction ZIP inspection, manifest/checksum/branch/destination validation, server-derived preview/challenge recomputation, and fake-only tests; no real restore was executed |
| Cleanup/reset safety is client/legacy driven | POS-M03 and POS-M03R close the Agent boundary with canonical managed-root policy, required safety-root boundaries, symmetric protected/install overlap and reparse checks, server-derived preview/challenge, execute-time recomputation, locks, explicit partial outcomes, exact-target branch verification, and code-owned SQL scope; retained WinUI compatibility calls fail closed when policy is not configured |
| Downloader service-identity portability | POS-M04/POS-M04R harden the Agent boundary, connection-bound trigger transport, outcome semantics, and fake portability seams; representative LocalSystem/Session 0 SMB proof remains required |
| Remote trigger reconciliation/idempotency | POS-M04R2 records `OutcomeUnknown` after a dispatched trigger cannot be confirmed and prevents automatic retry; local operation idempotency is not remote idempotency, and no verified remote job-status/reconciliation contract is available |
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

POS-M03R - Maintenance Denylist & SQL Scope Safety Closure is complete. Cleanup now fails closed
when any required managed/data/protected/install safety-root category is empty or invalid, rejects
targets and allowed reparse destinations that overlap protected/install roots in either containment
direction, and keeps rejection evidence path-free. Branch reset now accepts only the
server-resolved branch database, verifies the branch against that exact database before reset, and
normalizes/deduplicates only the code-owned historical `Sales`, `CashierSessions`, and
`InventoryMovements` table scope. Focused Release coverage passed 20 Application and 15 Agent
maintenance/worker tests; complete Release validation passed 225 .NET tests, the solution build
passed with zero warnings/errors, and the retained WinUI `win-x64` publish passed. All validation
used fake/disposable infrastructure; no real cleanup, SQL reset, Windows service control,
device-state mutation, or Angular Maintenance UI work occurred.

POS-M04 - Downloader Backend & SMB Portability is complete. The Agent now accepts only validated
logical branch batches at `/api/v1/downloads/batches`, snapshots server-owned non-secret settings,
loads the RDB password from the DPAPI-backed secret store at execution time, and runs the existing
downloader behavior behind a bounded `downloader` lock/idempotency/audit boundary. Trigger HTTP
requests use an approved same-endpoint policy with manual redirect handling, DNS address checks,
bounded timeouts, and SSRF rejection. SMB paths are canonical-root constrained, safe filenames are
revalidated, connection ownership distinguishes scope-owned/pre-existing/no-credential/conflict
outcomes, and unpublished `.partial` files are cleaned without overwriting published artifacts.
Ready archives are checksum-registered through the existing principal-scoped opaque artifact
catalog, while operation, audit, and problem evidence remains logical and path/credential-free.
Validation is fake/disposable-only; the ADR-012 LocalSystem/Session 0 representative-device gate
remains open and is not inferred from the automated tests. Focused Release coverage passed 4 new
Application downloader tests, 14 Infrastructure security/SMB tests, 4 Agent downloader-contract
tests, 17 operation-registry tests, and 5 artifact-catalog tests; complete Release validation
passed 247 .NET tests, the solution build passed with zero warnings/errors, and the retained WinUI
`win-x64` publish passed.

POS-M04R - Downloader Connection-Bound SSRF & Post-Trigger Outcome Truth is complete. The trigger
transport now binds DNS policy to the actual socket through `SocketsHttpHandler.ConnectCallback`:
preflight and connection-time resolution reject unsafe/private hostname answers, IPv4-mapped
IPv6 unsafe addresses fail closed, and manual redirects receive the same revalidation without
disabling TLS certificate validation. `DbDownloadService.RunWithOutcomeAsync` owns the trigger
milestone and translates post-acceptance repository failures through the Domain
`BackupRepositoryException` boundary; `OperationWorker` uses that result instead of inferring
trigger truth from normal return/exception flow. Real worker-path tests verify rejected triggers,
accepted-then-SMB failure, independent partial artifact outcomes, cancellation, REST, and audit
truth. Five new Infrastructure transport tests, one Application lifecycle test, and four Agent
worker tests passed; complete Release validation passed 257 .NET tests, the solution build passed
with zero warnings/errors, and retained WinUI `win-x64` publish passed. All evidence used fakes,
temporary streams, or temporary test infrastructure only; ADR-012 LocalSystem/Session 0 SMB
representative-device evidence remains open.

POS-M04R2 - Remote Trigger Uncertainty Truth Closure is complete. The trigger dispatch boundary
now distinguishes pre-dispatch rejection, definitive remote rejection, positive acknowledgement,
and post-dispatch uncertainty. Cancellation, timeout, connection loss, response transport failure,
and local response-policy failure after dispatch map to `OutcomeUnknown` with the sanitized
`downloader.trigger_outcome_unknown` code; unknown outcomes stop before SMB discovery and artifact
publication, and the explicit state plus retry guidance survives REST, operation retention, and
audit. Three new Infrastructure dispatch/transport tests, one Application no-SMB lifecycle test,
one Agent worker REST/audit test, and one contract sanitization test were added; complete Release
validation passed 263 .NET tests, the solution build passed with zero warnings/errors, and the
retained WinUI `win-x64` publish passed. All evidence used fakes, temporary streams, or temporary
test infrastructure only; ADR-012 LocalSystem/Session 0 SMB representative-device evidence and
remote trigger reconciliation/idempotency capability remain unverified.

## 15. POS-M05 reviewed repository baselines and evidence

POS-M05 was a documentation-only landing and collision audit. POS-M05R adds the corrective
runtime, repository-cleanliness, and integration-contract closure recorded below. POS-M05/M05R did
not move or modify POS source, generated Angular output, or Support Hub files. POS-M06 made only two
narrow frozen-Angular lint-hygiene corrections; it did not add a feature or modify Support Hub. The initial R1 read-only
review was at Support Hub `36a0eaa4d42a7dc1c2cb92df4daadc35f7abe5f0`; Support Hub `main` advanced
before closure, so the live counterpart document and capability model were re-read at the current
head below on 2026-08-10:

| Repository | Remote | Local checkout | Exact reviewed `main` head | Review state |
| --- | --- | --- | --- | --- |
| POS maintenance tool | `Hossam1104/pos_maintainance_tool` | `D:\AI Tools\DBS\pos_maintainance_tool` | `d73d0d9d2c2ea5b6138b261a31b08f20185dbb44` | Clean and synchronized before the POS-M06 branch was created |
| RMS+ Support Hub | `Hossam1104/Rms-Support-Hub` | `D:\AI Tools\DBS\Rms-Support-Hub` | `2a4a38aba2113f30c5751eb7b1fbf8a6cb13a91b` | Current remote `main` verified at this SHA; local checkout was clean and synchronized; read-only review only, no Support Hub files changed by POS-M06 |

The Support Hub checkout is not the historical placeholder-only snapshot described by the early
POS intake note. At the reviewed head it contains a real .NET 10 backend with
`RmsSupportHub.Core -> RmsSupportHub.Data -> RmsSupportHub.Api`, one backend xUnit project, and
an Angular 22 frontend with a live `/tools/pos-maintenance` informational placeholder. It does
not contain the POS Domain, Application, Infrastructure, Contracts, Agent, WinUI, or POS
operation/audit implementation. The old “source not supplied” intake remains historical evidence;
this audit uses the checked-out source as the current Support Hub structure.

The Support Hub backend currently has no Windows target, Negotiate registration, local-admin
authorization, antiforgery configuration, loopback binding, or POS Agent composition. Its API
uses controllers, `ExceptionMiddleware`, `SessionIdMiddleware`, CORS for the local Angular
development origin, OpenAPI, Dapper-backed module repositories, and a configurable outbound TLS
verification switch. Its frontend owns the shell, navigation, shared UI primitives, tokens,
branding, and the POS placeholder route. These facts make a privileged POS Agent boundary a
cross-project architecture decision rather than a safe file-copy operation.

`docs/POS_MAINTENANCE_INTEGRATION_READINESS.md` is a live counterpart document at the current
Support Hub `main`, not merely historical intake. It is valid as Support-Hub-side integration
input. Its direct Core/Data/Api placement recommendations are superseded for privileged POS
execution by the cross-project security review: a separate Windows POS Agent remains the
recommended privileged topology, while the final proxy/origin/deployment arrangement needs a
cross-project decision. The same readiness document records that the machine-local Agent is a
new architecture decision, Support Hub currently has no Agent identity/authorization boundary,
no generic execution surface is permitted, and POS must not silently widen the existing
outbound TLS bypass.

## 16. RMS+ Support Hub ownership boundary and recommended topology

The ownership boundary below is normative for preparation. The landing locations are candidates
for a later approved merge; they are not authorization to merge repositories or integrate Angular.

| Responsibility | POS owner and evidence | Support Hub owner and evidence | Preparation boundary |
| --- | --- | --- | --- |
| POS domain and application behavior | `src/PosAdminTool.Domain` and `src/PosAdminTool.Application`; models, policies, workflows, validation, and operation semantics | `backend/src/RmsSupportHub.Core`; general Support Hub domain behavior | POS capability remains isolated; Support Hub consumes an approved contract/module boundary |
| Privileged execution | `src/PosAdminTool.Infrastructure` and the Agent boundary; SQL, SCM, SMB, filesystem, cleanup/reset, restore, downloader, configuration, secrets | No privileged POS execution in the browser or current Core/Data modules | Keep Windows and machine-local adapters behind a POS-owned boundary |
| Agent security and runtime | `src/PosAdminTool.Agent`; loopback, Negotiate, local Administrators, antiforgery, redaction, bounded operations, SSE, audit | Current API composition in `backend/src/RmsSupportHub.Api/Program.cs`; no equivalent POS security boundary is present | Do not weaken POS security to fit the current general web host |
| API contracts | `src/PosAdminTool.Contracts/V1`; versioned DTOs, stable codes, Problem Details extensions, operation/artifact state | Support Hub controllers and current module DTOs/routes | Reconcile route, envelope, auth, and generated-client ownership in a cross-project decision |
| Angular shell and global visual system | `src/PosAdminTool.Web` is evidence only | `frontend/src/app`, shared components, layout, styles, themes, motion, branding, and tool registry | Support Hub owns the final shell and integrated POS route UX |
| POS feature acceptance | POS backend capabilities, safety invariants, state semantics, and acceptance criteria | Final route composition, interaction design, accessibility, and integrated feature screens | POS Web pages inform behavior only; no standalone Angular expansion |
| Retained desktop client | `src/PosAdminTool.WinUI` | None | Keep WinUI buildable and present until explicit cross-project cutover approval |

The recommended safe topology is a separately identifiable POS backend module with a separately
hosted Windows x64 local Agent (`RmsSupportHub.PosAgent` is a candidate name), reached through an
explicitly approved local transport or typed proxy. Collapsing the current Support Hub API and the
POS Agent into one process would require a security, target-framework, middleware, configuration,
service-identity, and deployment review. The final choice between a separate Agent process and a
shared host is `NEEDS CROSS-PROJECT DECISION`.

## 17. Project-level landing map

Every POS project and significant boundary is listed below. The `Disposition` column uses only the
canonical POS-M05 vocabulary. A disposition records a future merge action, not a change made in
this session.

| Current POS location | Intended Support Hub landing / owner | Disposition | Namespace, dependencies, DI/config/security, tests, generated/resources/build, and blocker |
| --- | --- | --- | --- |
| `src/PosAdminTool.Domain` | Candidate `backend/src/RmsSupportHub.Pos.Domain`; POS capability owner | KEEP WITH RENAME | Portable `net10.0`, no direct packages, no DI or generated output. Preserve model/enum/interface semantics and Domain tests. Namespace/project-name collision with `RmsSupportHub.Core` requires an isolated POS namespace. |
| `src/PosAdminTool.Application` | Candidate `backend/src/RmsSupportHub.Pos.Application`; POS capability owner | ADAPT DURING MERGE | `net10.0`, `Microsoft.Extensions.Logging.Abstractions` 10.0.10. Preserve injectable SQL/filesystem/SCM/SMB seams; adapt host logging and DI lifetimes. Application tests move with the module. `RestoreFileSystem` needs an explicit adapter-boundary review. |
| `src/PosAdminTool.Infrastructure` | Candidate `backend/src/RmsSupportHub.Pos.Infrastructure`; POS privileged backend owner | MOVE DURING MERGE | Windows TFM `net10.0-windows10.0.19041.0`; exact packages are `Microsoft.Data.SqlClient` 6.1.6, `Microsoft.Extensions.Logging.Abstractions` 10.0.10, `System.ServiceProcess.ServiceController` 10.0.10, and `System.Security.Cryptography.ProtectedData` 9.0.11. DI belongs to the approved POS host only. DPAPI, ACLs, LocalSystem, SMB, SSRF, and managed roots remain isolated. Infrastructure tests and Windows publish evidence move with it. |
| `src/PosAdminTool.Contracts` | Candidate `backend/src/RmsSupportHub.Pos.Contracts` or an explicitly isolated API contract area; shared API owner after review | KEEP WITH RENAME | `net10.0`, no direct package; preserve `V1`, camelCase/string enums, stable error codes, redaction, operation/artifact/file-handle contracts, and `/api/v1` semantics until route review. Contract tests move with the owner; generated clients are destination-derived. |
| `src/PosAdminTool.Agent` | Candidate separate `backend/src/RmsSupportHub.PosAgent`; POS privileged-host owner, or approved shared composition root | NEEDS CROSS-PROJECT DECISION | Windows Agent TFM; exact host packages are Negotiate 10.0.10, ASP.NET OpenAPI 10.0.10, Microsoft.OpenApi 2.7.5, and API description server 10.0.10. `Program.cs`, middleware, DI, service identity, loopback, auth, antiforgery, static files, and OpenAPI cannot be copied wholesale into current Support Hub API. Agent integration tests and generated API output depend on this decision. |
| `src/PosAdminTool.WinUI` | Retained POS project, outside the Support Hub frontend/backend landing | KEEP AS-IS | Windows x64 self-contained WinUI project with Windows App SDK 1.8.260710003, CommunityToolkit.Mvvm 8.4.2, and Microsoft.Extensions DI/logging 10.0.10. Keep XAML/resources/manifest/assets and publish workaround. No Support Hub DI, browser, generated output, or removal action is authorized. |
| `src/PosAdminTool.Web` shell and shared UI | `frontend/src/app` remains Support Hub-owned | DO NOT COPY - SUPPORT HUB ALREADY OWNS IT | POS Angular workspace is standalone SCSS with Barlow Condensed, Source Sans 3, IBM Plex Mono, and its own shell/styles. Support Hub Angular owns CSS, layout, tokens, Bootstrap Icons, Inter/JetBrains Mono, route registry, and shared components. POS shell, global styles, assets, and app composition stay reference material. |
| `src/PosAdminTool.Web` POS feature behavior and API mapping | `frontend/src/app/features/pos-maintenance/` after an approved feature contract | REFERENCE ONLY | `AgentApi`, page components, route behavior, accessibility tests, and e2e flows describe POS capability behavior. Adapt them to Support Hub HTTP/auth/error conventions only after route and transport decisions; do not copy standalone shell or feature files now. |
| `tests/PosAdminTool.Domain.Tests`, `Application.Tests`, `Infrastructure.Tests` | POS backend test areas under the selected POS module ownership | ADAPT DURING MERGE | Four project-level POS test dependencies use coverlet 6.0.4, xUnit 2.9.3, runner 3.1.4, and Test SDK 17.14.1; Agent integration additionally uses MVC.Testing 10.0.10. Preserve fake-only safety evidence, fixture isolation, exact-target checks, and no real destructive execution. |
| `tests/PosAdminTool.Agent.IntegrationTests` | POS Agent test assembly or shared host fixture area after topology decision | NEEDS CROSS-PROJECT DECISION | Test the real POS Agent composition, auth, antiforgery, contracts, operations, SSE, audit, artifacts, and endpoint mapping. Current Support Hub has one `RmsSupportHub.Tests` assembly and no equivalent Windows Agent fixture; do not flatten without an explicit owner. |
| POS root `PosAdminTool.sln`, `global.json`, `Directory.Build.props`, project lockfiles | POS build boundary first; later Support Hub solution/build orchestration | NEEDS CROSS-PROJECT DECISION | POS pins SDK 10.0.302, C# 13, exact package versions, and committed `packages.lock.json` files. Support Hub has `backend/RmsSupportHub.slnx` and no repository-wide POS-equivalent lockfile. Preserve Windows/OpenAPI/WinUI build gates while deciding solution ownership. |
| POS root/Web `.gitignore`, publish scripts, `run_app.cmd` | Destination ignore/build policy plus retained POS desktop tooling | ADAPT DURING MERGE | Preserve ignored `bin/`, `obj/`, `artifacts/`, Angular `dist`, `node_modules`, generated OpenAPI/client output, publish output, screenshots, logs, and local secrets. Support Hub’s `scripts/build.ps1` owns later orchestration; `run_app.cmd` remains POS-only until cutover. |
| POS `docs/`, `.ai/`, ADRs, session evidence, and preparation docs | POS programme documentation area; selected references only | REFERENCE ONLY | Keep current preparation plan/prompts authoritative for POS. Do not copy transcripts or historical migration files into Support Hub. Update the old Support Hub intake only in an authorized cross-project documentation task. |

## 18. File-level landing map

The following map expands the project map to the significant source areas that carry contracts,
privilege, runtime state, configuration, generated output, UI ownership, or test evidence.

### 18.1 Domain and Application

| Current location | Intended landing / owner | Disposition | Namespace/deps/DI/config/security/test/generated/resource/build/migration notes |
| --- | --- | --- | --- |
| `src/PosAdminTool.Domain/Models/**`, `Enums/**`, `Exceptions/**` | POS domain module under `RmsSupportHub.Pos.Domain` | KEEP WITH RENAME | Keep models such as `AgentConfiguration`, `BackupJob`, `DatabaseResolver`, downloader/maintenance/restore outcomes, stable exception meaning, and string/value semantics. No direct dependencies, DI, config, generated files, or host resources. Preserve Domain tests and do not allow Support Hub DTOs to replace domain types. |
| `src/PosAdminTool.Domain/Interfaces/**` | POS capability ports consumed by Application and Infrastructure | KEEP WITH RENAME | Interfaces for SQL, service manager, filesystem, SMB, backup API, configuration, crypto, connectivity, restore verification, and maintenance remain the portability seams. Keep them free of ASP.NET/Windows implementation types; test fakes remain owner-local. |
| `src/PosAdminTool.Application/Maintenance/**`, `Services/CleanupService.cs` | POS maintenance application module | ADAPT DURING MERGE | Preserve server-owned managed-root policy, preview/challenge/recomputation, SQL scope, locks, partial/residue truth, and sanitized outcomes. DI is host-owned; browser paths remain prohibited. Adapt compatibility façade only after endpoint/contract ownership is settled. |
| `src/PosAdminTool.Application/Restore/**`, `Services/RestoreService.cs` | POS restore application module | ADAPT DURING MERGE | Preserve archive safety, preview/challenge/execute-time policy, SQL MOVE planning, rollback/recovery truth, cancellation, locks, post-restore verification, and audit evidence. `RestoreFileSystem.cs` directly represents a filesystem adapter and requires boundary review before landing. |
| `src/PosAdminTool.Application/Services/BackupService.cs` | POS backup use-case module | ADAPT DURING MERGE | Preserve fake-test seams, branch validation, SQL/file backup semantics, stable operation outcomes, and no browser path leakage. DI and operation registration belong to one POS host. |
| `src/PosAdminTool.Application/Services/DbDownloadService.cs` | POS downloader application module | ADAPT DURING MERGE | Preserve credential isolation, trigger milestones, `NotAttempted`/`Failed`/`Accepted`/`OutcomeUnknown`, no-SMB-after-unknown rule, cancellation, artifacts, and sanitized branch outcomes. Local idempotency remains distinct from remote trigger idempotency. |
| `src/PosAdminTool.Application/UseCases/**` | POS configuration/import/operation use-case module | ADAPT DURING MERGE | Preserve redacted configuration, non-secret legacy import, principal-scoped operation submission, and test-connection semantics. Host config and authentication must not be inferred from Support Hub’s current unauthenticated controller pipeline. |

### 18.2 Infrastructure and privileged boundary

| Current location | Intended landing / owner | Disposition | Namespace/deps/DI/config/security/test/generated/resource/build/migration notes |
| --- | --- | --- | --- |
| `src/PosAdminTool.Infrastructure/Configuration/**` | POS Windows infrastructure module | MOVE DURING MERGE | `ServiceOwnedDirectoryProvisioner`, JSON stores, migration/import, atomic writes, DPAPI secret store, and config service keep service-owned `%ProgramData%` paths, ACLs, and machine-scope secret separation. Register once in the POS host; no Support Hub `appsettings` or browser secret path may replace it. Preserve Infrastructure tests. |
| `src/PosAdminTool.Infrastructure/Windows/SqlCmdExecutor.cs` | POS SQL adapter | MOVE DURING MERGE | Uses `Microsoft.Data.SqlClient` 6.1.6 and explicit `SqlConnection`/`SqlCommand` plans. Keep server-identity and fail-closed SQL inspection evidence; do not silently convert to Support Hub’s Dapper repository model. Existing `TrustServerCertificate = true` is a POS-owned SQL TLS decision requiring review before deployment to untrusted networks; it is not equivalent to the strict connection-bound HTTP trigger TLS boundary. ADR-012 service-identity evidence remains open. |
| `src/PosAdminTool.Infrastructure/Windows/WindowsServiceManager.cs`, `AdminPrivilegeManager.cs`, `ConnectivityMonitor.cs` | POS Windows/SCM adapters | MOVE DURING MERGE | Windows-only process/service control, administrator checks, and connectivity belong behind Agent/WinUI DI. No current Support Hub Core/Data/API project has equivalent Windows target or privilege boundary. Preserve fake SCM tests and representative live-SCM gate. |
| `src/PosAdminTool.Infrastructure/Smb/**` | POS SMB adapter | MOVE DURING MERGE | Preserve canonical roots, scoped `WNetAddConnection2`/`WNetCancelConnection2` ownership, safe filenames, connection disposal, partial cleanup, and Session 0 evidence requirements. Keep exact packages and Windows TFM; do not merge into general Support Hub Data. |
| `src/PosAdminTool.Infrastructure/Http/**` | POS connection-bound HTTP adapter | MOVE DURING MERGE | Preserve endpoint policy, manual redirects, DNS/address validation at connection time, connection-bound sockets, timeout/cancellation, and stable repository failure codes. Do not substitute Support Hub’s `ApiClient` or its current `Outbound:VerifyTls=false` default without a security decision. |
| `src/PosAdminTool.Infrastructure/Backups/**` | POS physical backup/restore filesystem adapters | MOVE DURING MERGE | Preserve path policy, temporary/partial cleanup, archive handling, and injectable filesystem seams. Managed roots and ACL/resource naming need one POS owner and explicit collision review. |

### 18.3 Contracts and Agent host

| Current location | Intended landing / owner | Disposition | Namespace/deps/DI/config/security/test/generated/resource/build/migration notes |
| --- | --- | --- | --- |
| `src/PosAdminTool.Contracts/V1/Common/**` | Isolated POS API contract namespace | KEEP WITH RENAME | Keep `ErrorCodes`, `ProblemDetailsExtensionKeys`, `FreshnessState`, paging, evidence, camelCase/string-enum, and RFC Problem Details rules. Avoid collision with Support Hub’s existing error envelope and flat DTO namespaces. Contract serialization/shape tests remain mandatory. |
| `src/PosAdminTool.Contracts/V1/Operations/**`, `Activity/**`, `Artifacts/**`, `Session/**` | POS Agent protocol contract area | KEEP WITH RENAME | Preserve bounded operation state, event/activity/artifact IDs, session/antiforgery DTOs, correlation and redaction rules. Artifact IDs and opaque file handles remain principal/purpose scoped; no host paths or secrets. |
| `src/PosAdminTool.Contracts/V1/Backups/**`, `Restore/**`, `Maintenance/**`, `Downloader/**`, `Files/**`, `Services/**`, `Configuration/**`, `Device/**` | POS capability contract modules | KEEP WITH RENAME | Keep stable logical request shapes and outcome states. Route prefix/version and error-envelope adaptation are cross-project decisions; generated output must be regenerated from the approved destination contract. |
| `src/PosAdminTool.Agent/Program.cs` | Separate POS Agent composition root, or explicitly approved shared host | NEEDS CROSS-PROJECT DECISION | Current POS root configures loopback Kestrel, Negotiate/local-admin policy, antiforgery, CSP/correlation, OpenAPI, static fallback, all POS DI, workers, and service-safe content root. Runtime OpenAPI is mapped only in Development after POS-M05R; build-time `Microsoft.Extensions.ApiDescription.Server` generation remains enabled for the Angular client flow. `/health/live` and `/health/ready` remain unauthenticated loopback liveness/readiness probes with only fixed status values (`live`/`ready`); they do not expose environment, service names, paths, credentials, or privileged state. Current Support Hub `Program.cs` has different middleware, CORS, TLS, and service registration. Do not combine roots in M05. |
| `src/PosAdminTool.Agent/Authorization/**`, `Antiforgery/**`, `Correlation/**` | POS Agent security boundary | ADAPT DURING MERGE | Preserve local Administrators authorization, same-origin/loopback assumptions, mutation antiforgery, correlation headers, CSP, and safe Problem Details. Support Hub currently has no equivalent Negotiate/antiforgery setup; security must be additive and reviewed. |
| `src/PosAdminTool.Agent/Operations/**` | One POS operation engine in the approved host | ADAPT DURING MERGE | `OperationRegistry`, worker/work items, `ResourceLockSet`, retention, principal-scoped idempotency, cancellation, event state, and sanitized messages must have a single DI owner. Do not duplicate it beside Support Hub draft state or introduce SQLite/SignalR. |
| `src/PosAdminTool.Agent/Endpoints/**` | POS endpoint module or separate Agent controllers | ADAPT DURING MERGE | Preserve endpoint-specific auth/antiforgery and `/api/v1` contracts for compatibility until route review. Reconcile Support Hub controller namespace, `/api/modules/**` topology, exception envelope, middleware order, CORS, and OpenAPI ownership. |
| `src/PosAdminTool.Agent/Artifacts/**`, `Audit/**`, `Files/**` | POS resource/audit services | ADAPT DURING MERGE | Keep principal-scoped artifact catalog, opaque file handles, allowlisted browse roots, five-minute/single-use rules, sanitized destructive JSONL audit, and resource ACL policy. Support Hub’s `var/drafts` is unrelated and must not become the POS artifact/audit root. |
| `src/PosAdminTool.Agent/Device/**`, `Services/**`, `Configuration/**`, `Restore/**`, `Maintenance/**` | POS Agent capability orchestration | ADAPT DURING MERGE | Keep device diagnostics, Windows service monitor/control, service-owned config, restore uploads/challenges, and maintenance challenges server-owned. DI lifetimes, hosted workers, process identity, install/service names, ports, and resource roots require one host decision. |
| `src/PosAdminTool.Agent/appsettings*.json`, `Properties/**`, `LoopbackBinding.cs`, `RuntimeRetentionPolicy.cs` | POS Agent deployment/configuration boundary | NEEDS CROSS-PROJECT DECISION | Preserve loopback-only binding, bounded retention, service-safe content root, no tracked secrets, and exact Windows launch/publish behavior. Support Hub’s `AllowedHosts: "*"`, CORS, HTTP launch profile, and named SQL keys cannot be copied into the privileged host without review. |

### 18.4 Angular, WinUI, tests, generated output, and documentation

| Current location | Intended landing / owner | Disposition | Namespace/deps/DI/config/security/test/generated/resource/build/migration notes |
| --- | --- | --- | --- |
| `src/PosAdminTool.Web/src/app/app.*`, `routes.ts`, `core/**`, `shared/**`, `styles.scss` | Support Hub `frontend/src/app` shell/shared ownership | DO NOT COPY - SUPPORT HUB ALREADY OWNS IT | Support Hub’s Angular route topology, tool registry/model, shared UI, tokens, typography, motion, assets, and error/interceptor conventions are authoritative. POS `app.config`, shell DI, visual system, and global styles are reference only. |
| `src/PosAdminTool.Web/src/app/core/agent-api.service.ts` | Support Hub POS transport adapter after API decision | REFERENCE ONLY | POS CSRF bootstrap, `/api/v1`, SSE, artifact URLs, and Problem Details behavior inform the adapter. Browser auth, same-origin/CORS, route prefix, local Agent origin, and error-envelope mapping must be approved before implementation. |
| `src/PosAdminTool.Web/src/app/features/**` | Support Hub POS feature area after UX/contract review | REFERENCE ONLY | Device, settings, services, backups, and placeholder behavior provide capability acceptance evidence. Do not duplicate POS global navigation, cards, fonts, icons, or feature backlog; Support Hub owns the final integrated components and tests. |
| `frontend/src/app/core/models/pos-capability.model.ts` (Support Hub) | Support Hub frontend capability metadata | DO NOT COPY - SUPPORT HUB ALREADY OWNS IT | The existing `diagnostics`, `backup-restore`, `configuration`, `windows-services`, and `environment-connectivity` categories are useful destination-side planning metadata. They do not replace POS backend contracts, authorization, operation, audit, or privileged execution boundaries. |
| `src/PosAdminTool.Web/openapi/**`, `src/PosAdminTool.Web/src/app/core/api/generated/**` | Destination-generated API output only | REFERENCE ONLY | These paths are generated/ignored and must not be hand-edited or copied. Regenerate only after the final Agent/API composition and route contract are approved. |
| `src/PosAdminTool.Web/package.json`, `package-lock.json`, `angular.json`, `tsconfig*.json`, scripts, e2e specs | Support Hub frontend toolchain; POS reference for capability tests | REFERENCE ONLY | POS pins Angular 22.0.8/CDK 22.0.6, npm 12.0.1, Node 24.18, TypeScript 6.0.3, and its own fonts/test tooling. Support Hub owns its package manifest/lock and Angular workspace; do not merge dependency sets in M05. |
| `src/PosAdminTool.WinUI/**`, `src/PosAdminTool.WinUI/PosAdminTool.WinUI.csproj`, `run_app.cmd` | Retained POS desktop boundary | KEEP AS-IS | Keep Windows App SDK resources, XAML, manifest, view models, service/configuration/log/operation screens, assets, and x64 publish workaround. No browser or Support Hub dependency is introduced; removal is a future dedicated decision. |
| `README.md`, `MIGRATION_GUIDE.md` | POS repository documentation/reference | REFERENCE ONLY | These root documents may explain POS history or usage, but they must not silently become authoritative RMS+ Support Hub documentation after integration. Use the canonical preparation plan and approved cross-project ADRs for future integration decisions. |
| `.artifacts/` | None; generated publish output | RETIRE LATER | POS-M05R removed 405 tracked historical publish artifacts totaling 164,321,637 bytes (156.71 MiB) from the current tree. Do not copy generated output; preserve only textual evidence/state. Historical commits may still contain old build artifacts. |
| `host_trace.txt`, `trace.txt` | None; runtime/debug output | RETIRE LATER | POS-M05R removed these tracked root debug leftovers and added narrow root ignore rules. Do not copy traces or logs; remove any recurrence before integration. |
| `tests/PosAdminTool.Domain.Tests/**`, `Application.Tests/**`, `Infrastructure.Tests/**` | POS module-owned unit/safety tests | ADAPT DURING MERGE | Preserve fake SQL/filesystem/SCM/SMB/crypto/configuration coverage, exact package versions, deterministic TimeProvider behavior, and fake/temp-only boundaries. Align namespaces and shared fixture ownership only after project layout is approved. |
| `tests/PosAdminTool.Agent.IntegrationTests/**` | POS Agent/shared-host integration test boundary | NEEDS CROSS-PROJECT DECISION | Preserve WebApplicationFactory, auth, contract shape/serialization, endpoint, worker, operation, SSE, artifact, audit, and security coverage. Current Support Hub one-test-project topology is not evidence that these boundaries can be flattened safely. |
| POS `.ai/**`, `docs/POS_SUPPORT_HUB_MERGE_PREPARATION_PLAN.md`, `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`, ADRs | POS programme records; selected references in future shared docs | KEEP AS-IS | Update current state/history/active task as memory, not source. Keep ADR-012 and the remote-trigger evidence gates visible. Do not copy raw logs, prompts outside the canonical runbook, or full diffs. |

### 18.5 POS-M05R R1 corrections and explicit dispositions

The current live Support Hub document and source were read read-only at clean, synchronized Support
Hub `main` `2a4a38aba2113f30c5751eb7b1fbf8a6cb13a91b` after its independent Order Requests/date-picker
frontend work. The canonical cross-project disposition is:

- **Support Hub readiness document:** `VALID AS SUPPORT-HUB-SIDE INTEGRATION INPUT`.
- **Its direct Core/Data/Api placement recommendations:** `SUPERSEDED FOR PRIVILEGED POS EXECUTION BY CROSS-PROJECT SECURITY REVIEW`.
- **Final privileged topology:** `SEPARATE WINDOWS POS AGENT RECOMMENDED`.
- **Final proxy/origin/deployment arrangement:** `NEEDS CROSS-PROJECT DECISION`.

The Support Hub readiness document's capability model at
`frontend/src/app/core/models/pos-capability.model.ts` is Support-Hub-owned. Its current
diagnostics, backup/restore, configuration, Windows-services, and environment/connectivity
categories are useful destination-side planning metadata, but they do not replace POS backend
contracts, Windows identity, authorization, operation state, artifact ownership, audit, or
privileged execution.

The following cross-project findings are explicit integration constraints:

| Finding | Disposition |
| --- | --- |
| Support Hub raw exception envelope | The current `backend/src/RmsSupportHub.Api/Middleware/ExceptionMiddleware.cs` sends `ex.Message` in an unhandled HTTP 500 envelope. It must not wrap privileged POS Agent endpoints unchanged. Any future proxy/shared boundary must preserve safe Problem Details, stable POS error codes, correlation ID, redaction, and no raw exception text. |
| Support Hub session cookie | `SessionIdMiddleware` issues the general browser `oot_sid` draft/session cookie. It is not a POS identity and must never substitute for the Windows principal, principal-scoped idempotency, artifact ownership, file-handle ownership, or destructive audit identity. POS identity remains Windows/principal-owned unless a future reviewed architecture provides an equivalent stronger contract. |
| POS SQL TLS | `TrustServerCertificate = true` in `SqlCmdExecutor` is an `EXISTING POS-OWNED SECURITY DECISION / REVIEW REQUIRED BEFORE DEPLOYMENT TO UNTRUSTED NETWORKS`. It is documentation-only in POS-M05R and is not equivalent to strict certificate validation on the connection-bound POS HTTP trigger path. |
| Runtime OpenAPI | POS runtime OpenAPI is Development-only after POS-M05R. `Microsoft.Extensions.ApiDescription.Server` and `OpenApiGenerateDocumentsOnBuild` remain the build-time generation path; generated Angular/OpenAPI files remain derived outputs and are not hand-edited or copied. |
| Health endpoints | `/health/live` and `/health/ready` remain unauthenticated because they are simple loopback liveness/readiness probes. Their fixed status-only output is intentionally non-privileged and must not grow to include environment, service names, paths, credentials, or privileged state. |

The preferred integration strategy is a clean source snapshot/import after architecture freeze. The
future import must exclude `.artifacts/`, `bin/`, `obj/`, Angular `dist/`, `node_modules/`,
generated OpenAPI/client output, traces/logs, the historical standalone Angular shell, historical
execution prompts, local secrets/configuration, and temporary uploads/downloads. Historical commits
may still contain old build artifacts. Therefore raw Git-history merging into RMS+ Support Hub is
not authorized; the original POS repository remains read-only historical evidence and important
POS SHAs should be referenced from a future approved integration ADR.

## 19. Namespace, route, contract, and error compatibility

Until an approved cross-project implementation task, retain the `PosAdminTool.*` namespaces,
`/api/v1` contract semantics, and generated-file boundaries. A mass namespace rename during
preparation would hide ownership and make review of the privileged boundary harder. The preferred
future namespace is an isolated `RmsSupportHub.Pos.*` family (or an equally explicit `PosAdminTool`
module namespace), with a separate `RmsSupportHub.PosAgent` host if the separate-process topology
is chosen. This remains `NEEDS CROSS-PROJECT DECISION`.

The current route topologies are materially different:

| Concern | POS current contract | Support Hub current contract | Required decision before implementation |
| --- | --- | --- | --- |
| Browser route | POS standalone routes such as `device`, `settings`, `services`, `backups` | Published `tools/pos-maintenance` placeholder route | Support Hub keeps the published route and owns final lazy feature composition |
| API route | POS `/api/v1` with session, antiforgery, device, service, operation, backup, restore, maintenance, downloader, file, artifact, activity, and event endpoints | Support Hub controllers under `/api/modules/**` plus existing module routes | Separate Agent origin/transport, explicit `/api/pos/v1` mapping, or another approved compatibility boundary |
| Authentication | Same-origin loopback Windows Negotiate and local Administrators | Current API has no visible Negotiate/auth registration; local Angular CORS is enabled | Decide where Windows identity is enforced; do not expose privileged endpoints through the current general API by default |
| Mutation protection | POS antiforgery cookie/header and local-admin policy | Support Hub current middleware does not provide the POS antiforgery contract | Preserve POS mutation protection or approve an equivalent reviewed scheme |
| Errors | RFC Problem Details with camelCase `correlationId` and stable `errorCode` | Support Hub `ExceptionMiddleware`/controller envelope and `SessionIdMiddleware` | Add an explicit adapter or preserve a separate POS contract; do not silently translate away codes/evidence |
| JSON and enums | camelCase properties and string enums, redacted paths/secrets | camelCase/string-enum conventions also exist, but DTO/envelope namespaces differ | Use isolated POS DTOs and contract tests; collision by serialization shape is still possible |
| OpenAPI/client | Agent build generates POS OpenAPI and Angular client | Support Hub has no POS generated client and owns its Angular build | One approved destination document/client; generated outputs remain derived |

POS contract invariants that must survive any later adaptation are stable versioning, logical IDs
instead of host paths, no browser secrets, principal-scoped idempotency, bounded operation state,
explicit downloader trigger state, correlation/error codes, and sanitized audit evidence. POS-M05R
corrects the trigger boundary conservatively: after dispatch, every received non-success HTTP
response is `OutcomeUnknown` with `downloader.trigger_outcome_unknown` because no authoritative
remote response contract proves that a status code is side-effect-free. A successful 2xx remains
`Accepted`; pre-dispatch validation, cancellation, endpoint-policy/SSRF rejection, and a first
connection rejection before HTTP bytes are sent remain `NotAttempted`. Unknown is terminal, does
not retry, discover SMB output, or publish artifacts, and requires the operator to check remote
backup state before retrying. No local Agent idempotency claim is a remote idempotency contract.

## 20. Exact dependency, framework, and build collision map

The following versions were read from the POS project files/lockfiles and the Support Hub project
files/lockfile. They are recorded for a later compatibility review; POS-M05 does not upgrade,
centralize, or regenerate dependencies.

| Area | POS exact baseline | Support Hub exact/current baseline | Collision and required ownership |
| --- | --- | --- | --- |
| SDK/language | `global.json` SDK 10.0.302; C# 13 via `Directory.Build.props`; nullable/implicit usings; committed NuGet lockfiles | .NET 10 projects; no POS-equivalent repository-wide `global.json` or `packages.lock.json` was found | Keep POS C# 13 and lockfile intent. Selecting a shared SDK/central package policy is `NEEDS CROSS-PROJECT DECISION`; no toolchain change in M05. |
| Target frameworks | Portable projects `net10.0`; Infrastructure/Agent/WinUI and relevant tests `net10.0-windows10.0.19041.0`; WinUI x64/win-x64 | Core/Data/Api/tests all `net10.0`; no current Windows-specific project | Windows APIs, service control, DPAPI, WNet SMB, and WinUI require a separate Windows-targeted POS boundary. Do not retarget Support Hub Core/Data/API blindly. |
| SQL client/data access | POS `Microsoft.Data.SqlClient` 6.1.6; direct `SqlConnection`/`SqlCommand` in `SqlCmdExecutor` | Support Hub Data `Dapper` 2.1.79 and `Microsoft.Data.SqlClient` 7.0.2 | Keep POS SQL adapter isolated and choose one compatible client/data-access ownership only in a later review. Do not replace POS SQL safety plans with Dapper during merge preparation. |
| ASP.NET/OpenAPI | Agent `Microsoft.AspNetCore.Authentication.Negotiate` 10.0.10, `Microsoft.AspNetCore.OpenApi` 10.0.10, `Microsoft.OpenApi` 2.7.5, `Microsoft.Extensions.ApiDescription.Server` 10.0.10 | Api `Microsoft.AspNetCore.OpenApi` 10.0.10, `Microsoft.OpenApi` 2.11.0, `Serilog.AspNetCore` 10.0.0 | One host must own OpenAPI and middleware. Microsoft.OpenApi version selection, generated schema, and Negotiate integration require a host decision; do not copy either `Program.cs`. |
| Extensions/logging | POS uses Microsoft.Extensions 10.0.10 packages where direct; Agent built-in `ILogger` plus sanitized JSONL audit | Support Hub Api uses Serilog.AspNetCore 10.0.0 and its middleware/logging pipeline | Shared logging may be composed later, but POS destructive audit semantics, redaction, and retention remain POS-owned. Package centralization waits for compatibility evidence. |
| Windows/security | POS `System.ServiceProcess.ServiceController` 10.0.10 and `System.Security.Cryptography.ProtectedData` 9.0.11; Windows App SDK 1.8.260710003 and CommunityToolkit.Mvvm 8.4.2 in WinUI | No equivalent Windows service/DPAPI/WinUI package | Keep all Windows-only packages in POS Windows projects; never introduce Windows App SDK into Support Hub backend or browser. |
| Angular runtime | Angular packages 22.0.8, CDK 22.0.6, RxJS 7.8.2, npm 12.0.1, Node 24.18, TypeScript 6.0.3; POS fonts Barlow Condensed/Source Sans 3/IBM Plex Mono | Angular lock resolves 22.0.8, CDK 22.0.6, RxJS 7.8.2, npm 12.0.1, TypeScript 6.0.3; Support Hub owns Bootstrap Icons, Three.js, Inter/JetBrains Mono and its manifest | Shared Angular versions do not authorize merging workspaces. Support Hub `package.json`/lock, styles, assets, icon set, and budgets own the destination. POS fonts and global styles are not copied. |
| Frontend build shape | POS project `web`, SCSS, OpenAPI generation during Agent build, production and e2e scripts | Support Hub project `frontend`, CSS, offline production configuration, `scripts/build.ps1`, no POS generated client | One Angular workspace and one generated-client owner must be selected later. Preserve no-Node target-device invariant and Windows Agent/OpenAPI publish gates. |
| Test packages | coverlet 6.0.4, xUnit 2.9.3, runner 3.1.4, Test SDK 17.14.1; Agent integration MVC.Testing 10.0.10 | coverlet 6.0.4, xUnit 2.9.3, runner 3.1.4, Test SDK 17.14.1, Moq 4.20.72, MVC.Testing 10.0.10; one backend test project | Common versions reduce but do not remove fixture/lifetime collisions. Preserve POS four-project safety boundaries and decide whether/where to combine tests. |
| Lockfiles/generated outputs | POS `packages.lock.json` per project and Web `package-lock.json`; OpenAPI/client outputs ignored/generated | Support Hub frontend `package-lock.json`; no NuGet lockfiles and no POS OpenAPI client | Do not delete POS lockfiles or commit generated API output. Destination lock/generated policy is a later build decision. |

## 21. DI, configuration, logging, security, resources, scripts, and test ownership

| Boundary | Current owner and exact evidence | Collision / merge rule |
| --- | --- | --- |
| Composition root and DI | POS Agent `Program.cs` registers POS adapters, stores, use cases, operation registry, locks, workers, auth, antiforgery, and Windows services. Support Hub `Api/Program.cs` registers controllers, CORS, OpenAPI, DraftManager, SQL repositories, outbound `HttpClient`, and middleware. | There must be one explicit composition owner per POS service. Do not copy POS registrations into Support Hub without lifetime, Windows TFM, auth, and restart-state review. `TimeProvider`, operation state, artifacts, locks, and workers must not be registered twice. |
| Configuration and secrets | POS service-owned configuration under `%ProgramData%\DBS\PosAdminTool`, ACL-provisioned JSON, machine-scope DPAPI secret store, legacy non-secret importer, redaction, and service-safe content root. | Support Hub tracked appsettings has named empty connection keys, user-secret/local overrides, and `var/drafts`. Keep distinct stores and ACLs until an approved deployment/configuration decision; never surface POS secrets or absolute paths in browser contracts. |
| Authentication and authorization | POS loopback-only Kestrel, Windows Negotiate, local Administrators policy, session, antiforgery, CSP/correlation. | Current Support Hub API has no visible POS equivalent. A shared host must prove local-only binding and local-admin enforcement before any privileged route is exposed. CORS or `AllowedHosts: "*"` must not widen the POS boundary. |
| HTTP/SSRF and outbound calls | POS `ConnectionBoundSocketConnector`/`BackupApiClient` validates endpoints, DNS/address, redirects, timeouts, cancellation, and response boundary. | Support Hub `ApiClient` uses a global `Outbound:VerifyTls` setting that currently defaults false and logs a warning. Do not reuse it for POS trigger/download traffic without a security ADR and equivalent connection-bound policy. |
| Operation state and audit | POS bounded in-memory registry, worker, locks, retention, SSE transport, opaque artifacts, and sanitized destructive JSONL audit. | Support Hub `DraftManager`/`var/drafts` is not a POS operation registry. One POS engine and one audit/resource policy must be selected; no SQLite, SignalR, PWA, service worker, or IndexedDB. |
| SQL/SCM/SMB/filesystem | POS Infrastructure owns the interfaces/adapters and test fakes. | Support Hub Core/Data owns its general repositories; it must not receive POS Windows/SMB/DPAPI responsibilities by accidental project reference. Privileged adapters remain behind POS-owned DI. |
| Logging and errors | POS uses `ILogger`, correlation IDs, stable error codes, sanitized messages, and JSONL destructive audit. | Support Hub Serilog/ExceptionMiddleware can be an outer sink/adapter only if POS evidence, redaction, Problem Details, and audit retention survive. Middleware order and envelope mapping require tests. |
| Frontend shell and shared UI | Support Hub owns `frontend/src/app`, route/tool registry, layout, shared components, tokens, branding, fonts, icons, and motion. | POS Web shell, `styles.scss`, fonts, global components, and navigation are `DO NOT COPY - SUPPORT HUB ALREADY OWNS IT`. POS feature behavior is `REFERENCE ONLY` until Support Hub adapts it. |
| Resources and paths | POS owns managed roots, service-owned config, artifacts, audit files, SMB scopes, temporary archives, service names, loopback port, and WinUI resources. | Establish unique names/ACLs and cleanup ownership before merge. Support Hub `var/`, public assets, SQL keys, and module data must not be assumed to be POS resource roots. |
| Generated files | POS Agent build derives `Web/openapi/**` and Angular generated client paths; both are ignored. | Destination must have one explicit generated-client rule and generator. Never hand-edit, copy, or commit POS generated output during M05. |
| Build/publish scripts | POS requires Windows for OpenAPI generation, `npm ci`/Angular build during publish, no Node on target, and WinUI publish for runtime resource staging. | Support Hub `scripts/build.ps1` can orchestrate only after preserving the POS Windows/OpenAPI/WinUI gates. A plain build is not evidence for retained WinUI. |
| Test ownership | POS four xUnit projects plus Web unit/e2e tests cover safety, contracts, Agent, and UI behavior; Support Hub has one backend xUnit project plus Angular tests. | Keep POS fake/temp-only and representative-device gates attached to their owners. Any shared host test must cover auth, antiforgery, contracts, operation state, error envelopes, and resource isolation. |

## 22. Collision findings, residual risks, and cross-project decisions

### 22.1 Concrete collision findings

1. There are two ASP.NET composition roots, different middleware orders, different authentication
   states, different CORS/static-file behavior, and different OpenAPI package versions. A blind
   `Program.cs` merge is unsafe.
2. POS is Windows-targeted at the Infrastructure/Agent boundary while Support Hub’s current
   backend projects are portable `net10.0`. Windows APIs cannot be hidden in the current Core/Data
   ownership without changing the target boundary.
3. POS’s `Microsoft.Data.SqlClient` 6.1.6 and explicit SQL plans collide with Support Hub Data’s
   Dapper 2.1.79 plus `Microsoft.Data.SqlClient` 7.0.2. The versions and data-access model require
   a deliberate compatibility decision.
4. POS Negotiate/local-admin/antiforgery/loopback security is stronger and materially different
   from the current Support Hub API. CORS and `AllowedHosts: "*"` must not become a privileged POS
   exposure.
5. POS `/api/v1` and Support Hub `/api/modules/**` are different public route families; POS
   `ProblemDetails` extensions and Support Hub exception envelopes are different contracts.
6. POS operation registry, artifact catalog, file handles, audit, locks, and hosted workers must
   have one POS owner. Support Hub’s `DraftManager` and `var/drafts` are not substitutes.
7. Both Angular workspaces are Angular 22 but have different project names, manifests, styles,
   shared UI, assets, fonts, and generated-client assumptions. Version similarity does not remove
   shell or package ownership collision.
8. POS has committed NuGet lockfiles and generated-output exclusions that Support Hub does not
   currently mirror. Destination ignore/lock rules need an explicit build decision.
9. POS WinUI resources and Windows publish staging are independent of Support Hub frontend assets;
   retaining WinUI is a mandatory preparation invariant.
10. The old Support Hub intake says POS source was not supplied, but the reviewed Support Hub head
    has a real placeholder and backend. The intake is historical; it must not be used to skip the
    actual code/route/package/security audit.

### 22.2 Open evidence gates preserved through POS-M06

| Gate | Status and evidence requirement |
| --- | --- |
| ADR-012 LocalSystem / Session 0 SMB | OPEN. A representative isolated Windows device/server must prove managed-root behavior, `WNetAddConnection2`, SMB enumeration, newest-batch discovery, ZIP read/download, cancellation/timeout cleanup, and scoped disconnect under the proposed service identity. Fake tests do not close this gate. |
| Remote trigger reconciliation/idempotency | OPEN / UNVERIFIED. No verified remote job-status, reconciliation, or remote idempotency contract exists. Local Agent idempotency is not remote idempotency; `OutcomeUnknown` is a real post-dispatch state and operator-directed retry requires remote evidence. |
| HTTP rejection semantics | CORRECTIONS IMPLEMENTED — R1 FOLLOW-UP PASSED. No authoritative remote response contract was found. Every received non-success response after dispatch maps to terminal `OutcomeUnknown` with `downloader.trigger_outcome_unknown`; 2xx remains `Accepted`, pre-dispatch rejection remains `NotAttempted`, and no automatic retry/SMB/artifact path follows unknown. Remote reconciliation/idempotency remains `OPEN / UNVERIFIED`. |
| Live Agent operational evidence | OPEN. Live loopback, Windows Negotiate/local-admin, antiforgery, SSE, and browser evidence remain unavailable in the preparation environment. |
| SQL/SCM/restore/maintenance/downloader reality | OPEN. Existing evidence is fake/temp/disposable-only; no real destructive SQL, service, SMB, endpoint, or production operation was executed. |
| Support Hub integration topology | OPEN. Separate Agent versus shared host/proxy, deployment/service identity, API route/transport, browser auth/CORS/HTTPS, configuration, and audit ownership are not approved. |
| WinUI retention | OPEN BY DESIGN. WinUI remains present and must remain publishable until explicit cross-project review and a dedicated owner-approved cutover. |

### 22.3 POS-M06 final evidence audit

The final audit passed the preparation gates below. `PASS` means the source, architecture,
contracts, tests, and repository evidence are sufficient for a merge-ready candidate; it does not
close the representative-device, live-Agent, remote-reconciliation, or cross-project deployment
gates listed in section 22.2.

| Audit area | POS-M06 result and evidence |
| --- | --- |
| Domain/Application portability | PASS. Domain/Application target `net10.0` and contain no ASP.NET, Windows UI, service-control, DPAPI, SMB, or SQL-client host dependencies; privileged behavior remains behind ports. |
| Infrastructure isolation | PASS. SQL, SCM, filesystem, SMB, HTTP, configuration, DPAPI, and backup/restore adapters remain in Windows-targeted Infrastructure and are composed only by the Agent/WinUI boundaries. Real SQL/SCM/SMB evidence remains open. |
| Agent security | PASS for preparation evidence. Loopback binding, Negotiate/local-Administrators policy, antiforgery, correlation, CSP, redaction, safe Problem Details, and Development-only runtime OpenAPI are covered by source and focused tests; live browser/Windows evidence remains open. |
| Operation runtime boundedness | PASS. Registry, principal-scoped idempotency, events, activity, artifacts, file handles, locks, cancellation, worker cleanup, retention, and sanitized audit are bounded and covered by the 277-test Release suite. |
| Restore safety and truth | PASS for fake/temp-only evidence. Archive inspection, manifest/checksum policy, preview/challenge/execute-time recomputation, locks, cancellation, rollback/recovery truth, and post-restore verification remain server-owned; no real restore was executed. |
| Maintenance safety and truth | PASS for fake/temp-only evidence. Managed-root/protected-root/reparse policy, server-derived preview/challenge, execute-time recomputation, locks, exact-target SQL scope, audit, and partial outcomes remain enforced; no real mutation was executed. |
| Downloader SSRF/SMB/trigger truth | PASS for fake/disposable-only evidence. Connection-bound SSRF/redirect checks, credential isolation, cancellation, artifacts, SMB ownership, and `NotAttempted`/`Accepted`/`OutcomeUnknown` truth are covered; ADR-012 and remote reconciliation remain open. |
| Configuration, secrets, and redaction | PASS. Service-owned ACL-restricted configuration, machine-scope DPAPI secrets, write-only browser secret fields, logical IDs, and sanitized operation/audit messages prevent browser path/secret leakage. |
| Contracts and generated files | PASS. Versioned V1 DTOs, string-enum/camelCase serialization, Problem Details extensions, contract-shape tests, build-generated OpenAPI, and ignored generated client output are consistent; no generated file is hand-edited or tracked. |
| Repository hygiene and landing map | PASS. Current tracked source has zero `.artifacts`, traces, `bin`, `obj`, Angular `dist`, `node_modules`, or generated OpenAPI/client paths; the file-level landing/collision map is recorded and standalone Angular expansion is frozen. |
| Retained WinUI | PASS. `PosAdminTool.WinUI` remains in the solution and the required `win-x64` Release publish completed successfully. Removal remains open by design. |
| Documentation and Git consistency | PASS. POS `main` and Support Hub `main` were independently resolved clean and synchronized; current state, plan, history, task gate, open evidence, and no-merge boundary are aligned. |

### 22.4 Cross-project decisions remaining after POS-M06

The following require an owner-approved cross-project review and are not silently resolved by this
audit: separate `PosAgent` process versus shared Support Hub host; final project and namespace
names; `/api/v1` versus an explicit integrated route prefix; local transport, browser auth, CORS,
HTTPS, and loopback enforcement; SQL client/data-access ownership; configuration/secret/DPAPI and
ACL ownership; logging and audit sink/retention; Windows service/install names and ports; Agent
deployment topology; operation/artifact/resource ownership; OpenAPI and generated-client
destination; Angular feature route and capability mapping; test fixture/project ownership; package
and lockfile policy; asset/font/icon licensing; and the Claude Opus 5 R2 pre-integration review.

## 23. Deferred standalone work

| Historical direction | Disposition | Current preparation meaning |
| --- | --- | --- |
| Session 09 restore backend/archive hardening | KEEP AS-IS | Complete as POS-M02; retain backend/security requirements and no standalone Restore UI |
| Session 10 Restore UI | RETIRE LATER | Preserve behavior as future Support Hub integration acceptance criteria |
| Session 11 cleanup/reset | ADAPT DURING MERGE | Backend path policy, preview, challenge, locks, audit, and tests are complete as POS-M03; standalone UI is not authorized |
| Session 12 DB Downloader | ADAPT DURING MERGE | Agent/backend, SMB, SSRF, credential, cancellation, artifact, and identity work is complete as POS-M04; standalone UI is not authorized |
| Session 13 UI polish/accessibility/release | RETIRE LATER | Do not execute as written; retain relevant backend/security/reliability criteria for Support Hub |
| Session 14 installer/cutover | RETIRE LATER | Do not finalize standalone installer or production cutover; retain WinUI and Windows App SDK |

## 24. Definition of Merge-Ready Candidate

POS may be called a **merge-ready candidate** only when POS-M01 through POS-M05 are complete,
Claude Opus 5 R1 has reviewed the result and its findings are addressed or explicitly accepted,
and POS-M06 passes its review-gated audit. POS-M06 has now passed. The candidate must demonstrate:

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

## 25. Preparation programme status

| Item | Status | Gate / outcome |
| --- | --- | --- |
| POS-M01 | Complete | Runtime boundedness, cleanup, artifact lifecycle, focused tests, full Release validation, and retained WinUI publish passed |
| POS-M02 | Complete | Backend restore/archive hardening only; focused restore tests, full Release solution gates, and retained WinUI publish passed; no real restore/config/service operation was executed |
| POS-M02R | Complete | Corrective Restore outcome semantics; fail-closed SQL inspection, explicit partial/cancellation/rollback/restart outcomes, stable Agent/audit failure evidence, focused fake-only tests, 178 Release .NET tests, and retained WinUI publish passed |
| POS-M02R2 | Complete | Restore terminal-result race closed; finalized service outcomes map directly through the Agent and remain stable under late cancellation, with focused 44 Application and 106 Agent tests, 182 Release .NET tests, and retained WinUI publish passed |
| POS-M02R3 | Complete; early Opus Restore follow-up passed | Interrupted SQL destructive truth, recovery-required partial semantics, worker-level Restore wiring, sanitized mode/target audit evidence, and positive bare-BAK branch evidence; focused 23 Application Restore and 24 Agent Restore/worker/audit tests, 190 Release .NET tests, and retained WinUI publish passed |
| POS-M03 | Complete; owner-authorized after the early Opus Restore follow-up gate passed | Cleanup/reset backend safety only: canonical managed-path policy, server-derived preview/challenge, recomputation, locks, partial/residue truth, sanitized audit, and fake-only worker-path coverage; focused 9 Application and 11 Agent maintenance tests, 210 Release .NET tests, zero-warning solution build, and retained WinUI publish passed |
| POS-M03R | Complete; corrective closure after POS-M03 | Required cleanup safety-root boundaries, symmetric protected/install overlap including allowed reparse destinations, exact server-approved branch database verification, and code-owned historical reset-table scope; focused 20 Application and 15 Agent maintenance/worker tests, 225 Release .NET tests, zero-warning solution build, and retained WinUI publish passed; fake/disposable-only with no real mutation |
| POS-M04 | Complete; owner-authorized | Downloader backend/SMB portability only; focused downloader/security/operation/artifact coverage, 247 Release .NET tests, zero-warning solution build, and retained WinUI publish passed; ADR-012 LocalSystem/Session 0 representative-device gate remains open |
| POS-M04R | Complete; owner-authorized corrective closure | Connection-bound trigger SSRF policy, explicit post-trigger outcome truth, stable repository failure boundary, five Infrastructure transport tests, one Application lifecycle test, four real Agent worker tests, 257 Release .NET tests, zero-warning solution build, and retained WinUI publish passed; no real endpoint/SMB/Session 0 evidence |
| POS-M04R2 | Complete; owner-authorized corrective closure | Explicit pre/post-dispatch trigger truth with `OutcomeUnknown`, safe terminal/no-SMB/no-retry behavior, browser/audit state and guidance, six focused new tests, 263 Release .NET tests, zero-warning solution build, and retained WinUI publish passed; ADR-012 and remote reconciliation/idempotency gates remain open |
| POS-M05 | Complete; owner-authorized | Exact POS/Support Hub baseline review, actual Support Hub structure inspection, project/file landing map, namespace/dependency/DI/config/security/test/generated/resource/build collision audit, residual gates, and cross-project decision list recorded. Claude Opus 5 R1 review subsequently passed before POS-M06. |
| POS-M05R | Complete; owner-authorized corrective closure | R1 safety, repository-cleanliness, and integration-contract corrections: post-dispatch non-success HTTP responses are terminal `OutcomeUnknown`; focused Infrastructure/Worker/OpenAPI tests added; full Release validation passed 277 .NET tests, a 0-warning solution build, and retained WinUI publish; 405 tracked `.artifacts/` files totaling 164,321,637 bytes and tracked root traces removed; live Support Hub readiness/capability ownership, raw-error/session-identity boundaries, SQL TLS disclosure, health/OpenAPI behavior, clean snapshot strategy, and open gates recorded. Claude Opus 5 R1 follow-up review passed. |
| R1 | Complete — follow-up PASS | Claude Opus 5 R1 follow-up review confirmed closure of the corrected landing/collision audit, HTTP response semantics, repository cleanliness, and open evidence-gate treatment; owner authorization was then supplied for POS-M06. |
| POS-M06 | Complete; merge-ready candidate | Final audit passed the preparation gates; Release build/test/publish, Angular unit/lint/build/backup-E2E, generated-output, memory, cleanliness, and diff checks passed. Representative-device, live-Agent, remote-reconciliation, and cross-project topology evidence remain explicitly open. |
| R2 | Required before integration; no execution authorized | Claude Opus 5 R2 final pre-integration review of the POS-M06 candidate and all open evidence/cross-project decisions |
| Repository merge | POS session merge complete; cross-project merge not authorized | The owner-authorized POS-M06 session branch was merged to POS `main`. RMS+ Support Hub remains untouched; no cross-project repository merge is authorized. |

At successful POS-M06 completion, stop with:

```text
POS-M06:
COMPLETE

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

RMS+ SUPPORT HUB INTEGRATION:
NOT AUTHORIZED

CLAUDE OPUS 5 R2:
REQUIRED BEFORE INTEGRATION

ADR-012 LOCAL SYSTEM / SESSION 0 SMB:
OPEN

REMOTE TRIGGER RECONCILIATION / REMOTE IDEMPOTENCY:
OPEN / UNVERIFIED

SUPPORT HUB FINAL DEPLOYMENT / PROXY TOPOLOGY:
OPEN

NEXT:
CLAUDE OPUS 5 R2 FINAL PRE-INTEGRATION REVIEW,
THEN OWNER-APPROVED CROSS-PROJECT INTEGRATION PLANNING
```
