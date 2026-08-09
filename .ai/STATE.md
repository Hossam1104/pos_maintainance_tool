# Current Project State

- **Updated:** 2026-08-10
- **Branch:** `main` after the verified POS-M04R downloader network/outcome corrective merge
- **Release or milestone:** .NET 10 + Angular 22 migration; Sessions 00-08 complete; standalone Angular expansion frozen; POS preparation programme active

## Working State

- `PosAdminTool.WinUI` remains the compatibility/parity baseline for configuration, service
  control, backup/restore, maintenance, downloader, and activity workflows.
- The solution contains Domain, Application, Infrastructure, Contracts, Agent, retained WinUI,
  Angular Web, and four xUnit test projects. The Agent is Windows-only, same-origin, loopback-only,
  Negotiate/local-Administrators authorized, antiforgery-protected, and DPAPI-backed for secrets.
- Long work uses the bounded in-memory Agent operation registry: REST is state truth, SSE is
  transport, idempotency is principal-scoped, named locks serialize conflicts, and destructive
  completions are appended to sanitized JSONL audit.
- POS owns backend/domain/security/privileged-operation and merge-readiness work. RMS+ Support Hub
  owns the final Angular shell, navigation, shared visual system, branding, themes, and integrated
  route experience. Standalone Angular expansion remains frozen.

## Verified Baseline and Earlier Gates

- Session 08 baseline: 125 .NET tests, 8 Angular tests in 6 files, backup E2E, and retained WinUI
  `win-x64` publish passed; no real SQL backup ran.
- POS-M01 bounded runtime state and cleanup corrections passed 141 .NET tests and WinUI publish.
- POS-M02 restore/archive hardening and corrective checkpoints POS-M02R/POS-M02R2/POS-M02R3 are
  complete; POS-M02R3 passed 23 Application Restore tests, 24 Agent Restore/worker/audit tests,
  190 full .NET tests, and WinUI publish with fake/temp-only infrastructure.
- The early Claude Opus 5 Restore follow-up gate was cleared by explicit owner authorization before
  POS-M03. POS-M03 did not redesign or broaden Restore.

## Verified POS-M03 Result

- `MaintenanceService` and Agent maintenance endpoints enforce canonical managed-root policy,
  protected/install/data-root separation, environment and drive-relative/UNC validation,
  reparse/junction/symlink defense, root-target rejection, server-derived previews, fresh
  principal-bound one-use challenges, typed logical confirmation, principal-scoped idempotency,
  conflicting resource locks, and execute-time recomputation.
- Cleanup and branch-reset stages preserve per-stage/per-target attempted versus completed truth;
  interrupted file/SQL seams are partial/recovery-required, with logical target IDs, stable failure
  codes, residue evidence, and recovery guidance. Service, filesystem, and SQL calls remain behind
  injectable interfaces; no absolute paths, credentials, raw SQL, or exception text enter browser or
  audit evidence.
- Focused Release coverage passed 9 Application maintenance tests and 11 Agent maintenance/
  worker tests. Complete Release validation passed 210 .NET tests; solution build passed with zero
  warnings/errors; retained WinUI `win-x64` publish passed. Tests used disposable fakes/temp
  infrastructure only; no real cleanup, reset, service control, device mutation, or Angular
  Maintenance UI work occurred.

## Verified POS-M03R Corrective Closure

- `MaintenancePathPolicy` now requires non-empty valid ManagedRoots, DataRoots, ProtectedRoots, and
  InstallRoots for cleanup; a target or allowed reparse destination cannot contain or be contained
  by a protected/install root, and rejection evidence remains path-free.
- Branch reset now uses the server-resolved branch database as its only authority, verifies the
  branch against that exact database before execution, and limits configured tables to the
  code-owned historical `Sales`, `CashierSessions`, and `InventoryMovements` set; the legacy
  unscoped reset fallback is not used.
- Focused Release coverage passed 20 Application and 15 Agent maintenance/worker tests. Complete
  Release validation passed 225 .NET tests; solution build passed with zero warnings/errors; the
  retained WinUI `win-x64` publish passed. All checks used fakes/disposable infrastructure only;
  no real cleanup, SQL reset, service control, device mutation, or Angular Maintenance UI work
  occurred.

## Verified POS-M04 Result

- The Agent exposes only the backend `/api/v1/downloads/batches` trigger boundary: requests carry
  validated logical branch codes/idempotency, work items carry server-owned settings, the RDB
  password loads from DPAPI at execution time, and downloader operations use bounded locking,
  cancellation, per-branch outcomes, and sanitized audit. Trigger HTTP uses manual redirects plus
  connection-bound DNS policy; sockets reach only policy-approved addresses while logical
  hostname/TLS semantics remain intact.
- Downloader discovery preserves newest-folder selection, exact branch ZIP matching, bounded
  stable-size observation, independent timeout/cancellation/failure truth, and partial outcomes.
  Trigger HTTP uses approved manual-redirect/DNS/timeout SSRF policy; SMB uses canonical roots,
  safe path revalidation, explicit ownership, `.partial` cleanup, and opaque artifact cataloging.
- Focused Release coverage passed 4 new Application downloader tests, 14 Infrastructure
  security/SMB tests, 4 Agent downloader-contract tests, 17 operation-registry tests, and 5
  artifact-catalog tests. Complete Release validation passed 247 .NET tests; solution build passed
  with zero warnings/errors; retained WinUI `win-x64` publish passed. All checks used fakes,
  disposable adapters, or temporary local staging only; no production endpoint, real SMB share, or
  LocalSystem/Session 0 evidence test ran.

## Verified POS-M04R Corrective Closure

- `ConnectionBoundSocketConnector` is the production `SocketsHttpHandler.ConnectCallback` seam: it
  resolves at connection time, normalizes IPv4-mapped IPv6, rejects loopback/link-local/metadata,
  unsafe private hostname answers, and mixed unsafe candidate sets before socket/HTTP bytes; the
  logical endpoint remains intact for HTTP/TLS, and redirects are bounded and revalidated.
- `DbDownloadService.RunWithOutcomeAsync` owns `NotAttempted`, `Failed`, and `Accepted` trigger
  state. SMB/path/I/O failures cross the Domain `BackupRepositoryException` boundary, and the real
  `OperationWorker` preserves accepted-trigger truth through repository failure, partial artifact
  completion, and cancellation; REST/audit retain sanitized codes and opaque artifact IDs.
- Focused Release coverage passed 5 new Infrastructure connection-bound DNS/SSRF tests, 1 new
  Application lifecycle test, and 4 real Agent worker outcome tests. Complete Release validation
  passed 257 .NET tests; solution build passed with zero warnings/errors; retained WinUI `win-x64`
  publish passed. All checks used fakes, temporary streams, or disposable test infrastructure;
  no real backup endpoint, SMB server, service identity, device mutation, or Angular Downloader UI
  work occurred.

## Active Programme

- Canonical plan: `docs/POS_SUPPORT_HUB_MERGE_PREPARATION_PLAN.md`.
- Canonical prompts: `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`.
- `TASK.md` contains the complete POS-M05 landing/collision audit prompt pending owner authorization;
  POS-M04R is complete, POS-M05 requires the Claude Opus 5 R1 review outcome before POS-M06, and
  POS-M06 is review-gated.
- Repository merge beyond the authorized session lifecycle, Angular integration, standalone
  installer cutover, and WinUI removal are not otherwise authorized.

## Known Risks and Gates

- Managed-root behavior under LocalSystem, representative-device SCM control, and SMB Session 0
  behavior still require safe representative-device evidence. Fake tests do not replace that gate.
- Manual live-Agent SSE and real browser Negotiate/admin evidence remain operational evidence gaps.
- ADR-012's representative LocalSystem/Session 0 SMB gate remains open: installed-service proof on
  an isolated non-production device/server must demonstrate WNetAddConnection2, enumeration,
  newest-batch discovery, ZIP read/download, cancellation/timeout cleanup, and scoped disconnect;
  fake tests do not replace it.
- WinUI remains retained until cross-project RMS+ Support Hub review and explicit owner-approved dedicated cutover.
