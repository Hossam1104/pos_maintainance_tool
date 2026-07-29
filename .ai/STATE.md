# Current Project State

- **Updated:** 2026-07-30
- **Branch:** `migration/session-05`
- **HEAD:** `17a478b` (`feat: Implement service management endpoints and UI integration`)
- **Release or milestone:** .NET 10 + Angular 22 migration; Sessions 00-07 complete, Session 08 active

## Working State

- The retained WinUI application remains the functional parity baseline for configuration, service control, backup/restore, maintenance, downloader, and activity workflows.
- The migration solution includes Domain, Application, Infrastructure, retained WinUI, Contracts, Agent, Web, and four xUnit test projects.
- The Agent implements loopback hosting, Negotiate/admin authorization, antiforgery, safe Problem Details/correlation IDs, session discovery, allowlisted file browsing with opaque handles, secure configuration/DPAPI secrets, device diagnostics, redacted configuration endpoints, and the in-memory operation engine with SSE and destructive JSONL audit.
- The Angular Branch Signal Desk includes Agent-backed Overview, Device, Settings, and Services screens; Settings keeps secrets write-only and exposes managed browse roots without host paths.
- Session 07 adds opaque configured-service IDs, Agent-side status polling/SSE recovery, authorized and audited start/stop/restart commands, and accessible command state feedback. Automated SCM behavior uses a fake manager only.

## Active Blocker

- None. The user accepted the Session 07 GO gate on 2026-07-30 without a representative-device LocalSystem SCM control check.

## Known Risks

- Managed-root behavior under LocalSystem and SMB access in Session 0 still require representative-device proof; SMB Session 0 behavior is the highest environment risk.
- Manual live-Agent SSE smoke and a real browser Negotiate/admin round trip have not been recorded.
- Legacy WinUI cleanup and restore retain documented destructive-path and archive-validation weaknesses until Sessions 09 and 11 replace those flows safely.
- Representative-device LocalSystem SCM control remains unproven; the user accepted this risk in the Session 07 GO decision. No RMS or system service has been controlled.

## Recently Completed

- Sessions 00-07 are indexed in `.ai/HISTORY.md`.
- Session 06 completed the Agent-backed Overview, Device, and Settings parity flow with redacted configuration and safe browse roots.

## Next Recommended Task

- Execute `MIGRATION-SESSION-08` local backup workflow from `TASK.md`.
