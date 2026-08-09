# Current Project State

- **Updated:** 2026-08-09
- **Branch:** `main` after the verified POS-M04 downloader backend/SMB portability merge
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
  complete. POS-M02R3 passed 23 Application Restore tests, 24 Agent Restore/worker/audit tests,
  190 full .NET tests, and WinUI publish; fake/temp-only, with no real restore or device mutation.
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
  code-owned historical `Sales`, `CashierSessions`, and `InventoryMovements` set with
  case-insensitive deduplication. The legacy unscoped reset fallback is not used.
- Focused Release coverage passed 20 Application and 15 Agent maintenance/worker tests. Complete
  Release validation passed 225 .NET tests; solution build passed with zero warnings/errors; the
  retained WinUI `win-x64` publish passed. All checks used fakes/disposable infrastructure only;
  no real cleanup, SQL reset, service control, device mutation, or Angular Maintenance UI work
  occurred.

## Verified POS-M04 Result

- The Agent exposes only the backend `/api/v1/downloads/batches` trigger boundary: requests carry
  validated logical branch codes and idempotency, work items carry server-owned non-secret settings,
  the RDB password is loaded from the DPAPI-backed secret store at execution time, and downloader
  operations use the bounded `downloader` resource lock, cancellation, per-branch outcomes, and
  sanitized audit evidence.
- Downloader discovery preserves newest-created-folder selection, exact branch ZIP matching,
  bounded deterministic stable-size observation, independent timeout/cancellation/failure truth,
  and partial outcomes. Trigger HTTP uses approved endpoint/manual redirect/DNS/timeout SSRF policy;
  SMB uses canonical roots, safe filename/path revalidation, explicit connection ownership, and
  cleanup-safe `.partial` publication. Completed archives use the existing principal-scoped opaque
  artifact catalog.
- Focused Release coverage passed 4 new Application downloader tests, 14 Infrastructure
  security/SMB tests, 4 Agent downloader-contract tests, 17 operation-registry tests, and 5
  artifact-catalog tests. Complete Release validation passed 247 .NET tests; solution build passed
  with zero warnings/errors; retained WinUI `win-x64` publish passed. All checks used fakes,
  disposable adapters, or temporary local staging only; no production endpoint, real SMB share, or
  LocalSystem/Session 0 evidence test ran.

## Active Programme

- Canonical plan: `docs/POS_SUPPORT_HUB_MERGE_PREPARATION_PLAN.md`.
- Canonical prompts: `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`.
- `TASK.md` contains the complete POS-M05 landing/collision audit prompt pending owner authorization;
  POS-M05 requires the Claude Opus 5 R1 review outcome before POS-M06; POS-M06 is review-gated.
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
- WinUI remains retained until cross-project RMS+ Support Hub review and explicit owner-approved
  dedicated cutover.
