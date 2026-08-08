# Current Project State

- **Updated:** 2026-08-09
- **Branch:** `main` after the POS -> RMS+ Support Hub roadmap reconciliation merge
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
  Session 08 local backup workflow with principal-scoped artifact streaming.
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

## Active Programme

- Canonical plan: `docs/POS_SUPPORT_HUB_MERGE_PREPARATION_PLAN.md`.
- Canonical prompts: `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`.
- `TASK.md` contains the complete POS-M01 prompt and is prepared for owner authorization; POS-M01
  was intentionally not executed during reconciliation.
- POS-M01 is runtime-state boundedness and Session 08 documentation correction. POS-M02/M03/M04
  prepare restore, cleanup/reset, and downloader backends. POS-M05 produces the landing/collision
  audit and requires Claude Opus 5 R1 review. POS-M06 is review-gated; R2 precedes any integration.
- Repository merge, Angular integration, standalone installer cutover, and WinUI removal are not
  authorized.

## Known Risks and Gates

- `OperationRegistry` queue capacity is 32, but source inspection confirmed no bounded retention for
  `_entries`, `_idempotency`, completed records, per-entry events, or `ArtifactCatalog._entries`;
  this is scheduled for POS-M01 and was not changed in reconciliation.
- Managed-root behavior under LocalSystem, representative-device SCM control, and SMB Session 0
  behavior still require safe representative-device evidence.
- Manual live-Agent SSE and real browser Negotiate/admin evidence are not substitutes for the fake
  automated tests and remain operational evidence gaps.
- Legacy Restore/Cleanup/Downloader capabilities remain unsafe or incomplete for Agent exposure;
  use POS-M02 through POS-M04 only after their entry gates are met.
- WinUI remains retained until cross-project review and explicit owner-approved cutover.
