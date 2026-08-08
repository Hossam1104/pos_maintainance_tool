# Current Project State

- **Updated:** 2026-08-09
- **Branch:** `main` after the Session 08 merge
- **Release or milestone:** .NET 10 + Angular 22 migration; Sessions 00-08 complete, Session 09 ready

## Working State

- The retained WinUI application remains the functional parity baseline for configuration, service control, backup/restore, maintenance, downloader, and activity workflows.
- The migration solution includes Domain, Application, Infrastructure, retained WinUI, Contracts, Agent, Web, and four xUnit test projects.
- The Agent implements loopback hosting, Negotiate/admin authorization, antiforgery, safe Problem Details/correlation IDs, session discovery, allowlisted file browsing with opaque handles, secure configuration/DPAPI secrets, device diagnostics, redacted configuration endpoints, the in-memory operation engine with SSE and destructive JSONL audit, and the local backup operation with safe artifact streaming.
- The Angular Branch Signal Desk includes Agent-backed Overview, Device, Settings, Services, and Backups screens; Settings and Backups keep secrets and host paths out of browser contracts.
- Session 07 adds opaque configured-service IDs, Agent-side status polling/SSE recovery, authorized and audited start/stop/restart commands, and accessible command state feedback. Automated SCM behavior uses a fake manager only.

## Active Blocker

- None. The user accepted the Session 07 GO gate on 2026-07-30 without a representative-device LocalSystem SCM control check.

## Known Risks

- Managed-root behavior under LocalSystem and SMB access in Session 0 still require representative-device proof; SMB Session 0 behavior is the highest environment risk.
- Manual live-Agent SSE smoke and a real browser Negotiate/admin round trip have not been recorded.
- Legacy WinUI cleanup and restore retain documented destructive-path and archive-validation weaknesses until Sessions 09 and 11 replace those flows safely.
- Representative-device LocalSystem SCM control remains unproven; the user accepted this risk in the Session 07 GO decision. No RMS or system service has been controlled.

## Recently Completed

- Sessions 00-08 are indexed in `.ai/HISTORY.md`.
- Session 08 completed the Agent-backed local backup flow with preflight validation, compatibility retry, manifest/checksum artifacts, safe catalog/download contracts, operation recovery, and the select/review/run/result UI. Real SQL backup execution was not authorized or performed.

## Next Recommended Task

- Execute `MIGRATION-SESSION-09` restore backend and archive hardening from `TASK.md`.
