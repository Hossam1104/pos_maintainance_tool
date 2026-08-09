# Current Project State

- **Updated:** 2026-08-09
- **Branch:** `main` after the verified POS-M02R2 restore terminal-outcome correction merge
- **Release or milestone:** .NET 10 + Angular 22 migration; Sessions 00-08 complete; standalone Angular expansion frozen; POS preparation programme active

## Working State

- The retained WinUI application remains the functional compatibility/parity baseline for
  configuration, service control, backup/restore, maintenance, downloader, and activity workflows.
- The solution includes `PosAdminTool.Domain`, `PosAdminTool.Application`,
  `PosAdminTool.Infrastructure`, retained `PosAdminTool.WinUI`, `PosAdminTool.Contracts`,
  `PosAdminTool.Agent`, the Angular Web workspace, and four xUnit test projects.
- The Agent is loopback-only and implements Negotiate/local-Administrators authorization,
  antiforgery, safe Problem Details/correlation IDs, session/device/configuration/service/file
  contracts, opaque browse handles, DPAPI-backed service secrets, operations/SSE/audit, and the
  Session 08 local backup workflow with principal-scoped artifact streaming. POS-M01 adds
  injectable bounded retention for operation state, idempotency, events, activity, artifacts, and
  file handles, plus cancellation/resource/staging cleanup guarantees. POS-M02 adds the backend
  restore upload, browse-handle, archive-policy, preview/challenge, operation, and execute seams;
  standalone Angular Restore remains frozen.
- The existing Angular implementation includes Agent-backed Overview, Device, Settings, Services,
  and Backups screens. Restore, Maintenance, Downloads, and Activity remain placeholder routes;
  standalone expansion is frozen for RMS+ Support Hub integration.
- POS owns backend/domain/security/privileged-operation and merge-readiness work. RMS+ Support Hub
  owns the final Angular shell, global navigation, shared visual system, branding, themes, and
  integrated route experience.

## Verified Session 08 Baseline

- Recorded Session 08 gates: Release .NET build passed; Release .NET tests passed with 125 tests;
  Angular unit tests passed with 8 tests in 6 files; backup E2E passed with fake adapters; retained
  WinUI `win-x64` publish passed.
- No real SQL backup command was authorized or executed.
- Old Session 05 97/98-test statements are historical and must not be used as current status.

## Verified POS-M01 Result

- `RuntimeRetentionPolicy` defaults: 64 completed operations for one hour, 32 events per operation,
  64 activity records with active-operation visibility, 64 artifacts for 24 hours with active
  download leases, and 256 five-minute file handles. Inclusive clock boundaries are injectable in
  tests; valid artifact admission fails closed rather than deleting a valid download.
- Principal-scoped idempotency mappings are removed with evicted operations; queued/running entries
  are never evicted. Operation messages are bounded, newline-safe, and sanitized for paths, secrets,
  and exception text. Backup temporary, staging, and unpublished post-move archives are cleaned.
- Focused checks passed: Agent integration 88 tests and Application 21 tests. Full Release solution
  validation passed 141 .NET tests; retained WinUI `win-x64` publish passed.

## Verified POS-M02 Result

- Restore sources remain distinct: bounded streamed uploads are capped and cleaned through the
  existing retention model, while device files are selected only through principal/purpose-bound
  opaque browse handles. In-flight upload slots and bytes are reserved, so concurrent staging
  cannot exceed the configured count or byte bounds.
- ZIP metadata, entry names, sizes, expanded totals, compression ratios, allowed extensions,
  duplicate names, reparse/symlink indicators, manifest/schema/component mappings, checksums, and
  branch evidence are validated before private temporary extraction. SQL logical-file discovery,
  deterministic MOVE planning, server-owned configuration destinations, free-space checks, and
  rollback-capable configuration copies are behind testable seams.
- Execute-time policy is recomputed from server-owned settings/source state. The short-lived
  one-use challenge is principal-bound and fingerprint-bound to the source identity, mode, branch,
  target database, SQL MOVE plan, configuration overwrite set, services, archive/policy version,
  and confirmation text. Restore operations use the existing idempotency, cancellation, audit,
  resource-lock, and bounded operation architecture.
- Focused Release coverage passed 17 Application restore/planning tests and 13 Agent
  restore/upload/challenge tests. Complete Release solution validation passed 170 .NET tests with
  zero failures; retained WinUI `win-x64` publish passed. Tests used disposable fakes and temporary
  directories only; no real restore, RMS configuration overwrite, or Windows service stop ran.

## Verified POS-M02R Result

- SQL restore planning now fails closed when server-side `RESTORE FILELISTONLY` evidence is empty,
  null, malformed, or cannot produce a valid bounded data/log MOVE plan; no guessed logical names
  are sent to database restore execution.
- Restore execution records service-stop, database-restore, post-restore verification,
  configuration overwrite/rollback, and service-restart milestones. Database/configuration work
  that does not achieve the requested complete outcome is represented as `PartialSuccess` with
  stable sanitized failure codes; cancellation after confirmed database restore is not represented
  as ordinary cancellation.
- Configuration rollback success/failure is an explicit internal result. Recovery-required outcomes
  remain sanitized, and partial failure codes are retained by Agent operation details and destructive
  JSONL audit records. Targeted Release correction coverage passed 44 Application tests and 102
  Agent integration tests; complete Release solution validation passed 178 .NET tests, and the
  retained WinUI `win-x64` publish passed. All restore correction tests used fake adapters and
  temporary directories only.

## Verified POS-M02R2 Result

- `RestoreService` is authoritative for finalized terminal outcomes; `OperationWorker` maps the returned `OperationStatus` directly and no longer rewrites a successful Restore after late cancellation.
- Restore calls preserve finalized `Succeeded`, `PartiallySucceeded`, and `Failed` states through `Entry.Complete`; genuine service-level `Cancelled` remains cancelled and stable codes stay in detail/audit.
- Focused Release coverage passed 44 Application and 106 Agent integration tests; complete Release solution validation passed 182 .NET tests and retained WinUI publish passed. Fakes/temp directories only were used, with no real SQL, RMS configuration, or Windows service operation.

## Active Programme

- Canonical plan: `docs/POS_SUPPORT_HUB_MERGE_PREPARATION_PLAN.md`.
- Canonical prompts: `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`.
- `TASK.md` contains the complete POS-M03 prompt for the next owner authorization; POS-M01/POS-M02 and
  corrective checkpoints POS-M02R/POS-M02R2 are complete. POS-M03/M04 prepare cleanup/reset and downloader
  backends; POS-M05 produces the landing/collision audit and requires Claude Opus 5 R1 review; POS-M06
  is review-gated and R2 precedes any integration.
- Repository merge, Angular integration, standalone installer cutover, and WinUI removal are not authorized.

## Known Risks and Gates

- Operation runtime retention is now bounded by the verified POS-M01 policy. A full artifact catalog
  rejects new registration while valid slots are occupied; this is a safe capacity outcome, not an
  eviction of a legitimately downloadable artifact.
- Managed-root behavior under LocalSystem, representative-device SCM control, and SMB Session 0
  behavior still require safe representative-device evidence.
- Manual live-Agent SSE and real browser Negotiate/admin evidence are not substitutes for the fake
  automated tests and remain operational evidence gaps.
- Legacy Cleanup/Downloader capabilities remain unsafe or incomplete for Agent exposure; POS-M03
  and POS-M04 are still gated by their owner-authorized sessions. POS-M02/POS-M02R restore backend
  work, including POS-M02R2, is complete, but representative-device LocalSystem/Session 0 evidence
  remains outstanding.
- WinUI remains retained until the cross-project RMS+ Support Hub review and an explicit owner-approved
  dedicated cutover.
