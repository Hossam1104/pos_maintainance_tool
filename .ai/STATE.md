# Current Project State

- **Updated:** 2026-08-09
- **Branch:** `main` after the POS-M01 runtime boundedness merge
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
  file handles, plus cancellation/resource/staging cleanup guarantees.
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

## Active Programme

- Canonical plan: `docs/POS_SUPPORT_HUB_MERGE_PREPARATION_PLAN.md`.
- Canonical prompts: `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`.
- `TASK.md` contains the complete POS-M02 prompt for the next owner authorization; POS-M01 is
  complete. POS-M02/M03/M04 prepare restore, cleanup/reset, and downloader backends. POS-M05
  produces the landing/collision
  audit and requires Claude Opus 5 R1 review. POS-M06 is review-gated; R2 precedes any integration.
- Repository merge, Angular integration, standalone installer cutover, and WinUI removal are not
  authorized.

## Known Risks and Gates

- Operation runtime retention is now bounded by the verified POS-M01 policy. A full artifact catalog
  rejects new registration while valid slots are occupied; this is a safe capacity outcome, not an
  eviction of a legitimately downloadable artifact.
- Managed-root behavior under LocalSystem, representative-device SCM control, and SMB Session 0
  behavior still require safe representative-device evidence.
- Manual live-Agent SSE and real browser Negotiate/admin evidence are not substitutes for the fake
  automated tests and remain operational evidence gaps.
- Legacy Restore/Cleanup/Downloader capabilities remain unsafe or incomplete for Agent exposure;
  use POS-M02 through POS-M04 only after their entry gates are met.
- WinUI remains retained until the cross-project RMS+ Support Hub review and an explicit owner-approved
  dedicated cutover.
