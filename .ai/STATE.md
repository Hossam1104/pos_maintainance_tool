# Current Project State

- **Updated:** 2026-07-29
- **Branch:** `migration/session-05`
- **HEAD:** `ef7803a` (`feat(web): add Branch Signal Desk shell`)
- **Release or milestone:** .NET 10 + Angular 22 migration; Sessions 00–04 complete, Session 05 implementation complete but regression gate blocked

## Working State

- The retained WinUI application remains the functional parity baseline for configuration, service control, backup/restore, maintenance, downloader, and activity workflows.
- The migration solution includes Domain, Application, Infrastructure, retained WinUI, Contracts, Agent, Web, and four xUnit test projects.
- The Agent implements loopback hosting, Negotiate/admin authorization, antiforgery, safe Problem Details/correlation IDs, session discovery, allowlisted file browsing with opaque handles, secure configuration/DPAPI secrets, and the in-memory operation engine with SSE and destructive JSONL audit.
- The Angular Branch Signal Desk shell is implemented with local fonts/assets, semantic light/dark tokens, responsive navigation, accessible status/signal surfaces, lazy feature placeholders, a development-only gallery, unit tests, and Playwright accessibility coverage.
- Session 05 Angular lint, 5 unit tests, production build, local-asset audit, 3 Playwright checks, .NET build, and retained WinUI publish are recorded passing.
- The Session 05 full .NET gate remains open: the audit-record integration test passes in isolation but fails in the full run because `audit/operations.jsonl` is absent when read.
- The planned Windows Service installer and real operational Agent endpoints/screens are not implemented.

## Active Blocker

- `OperationEndpointTests.DestructiveDiagnostic_WritesExactlyOneSanitizedAuditRecord` is nondeterministic by execution context: targeted run passes 1/1; full solution run fails 1/98. Session 06 must not start until the full suite passes.

## Known Risks

- SQL, managed-root ACL, and SMB access under the proposed dedicated Windows Service identity require representative-device proof; SMB Session 0 behavior is the highest environment risk.
- Manual live-Agent SSE smoke and a real browser Negotiate/admin round trip have not been recorded.
- Legacy WinUI cleanup and restore retain documented destructive-path and archive-validation weaknesses until Sessions 09 and 11 replace those flows safely.
- The explicit Angular/Agent go/no-go evaluation remains due after Session 07.

## Recently Completed

- Sessions 00–04 are indexed in `.ai/HISTORY.md`.
- Session 05 implemented the Branch Signal Desk design system and shell at `ef7803a`; it is not in completed history until its standing regression gate closes.

## Next Recommended Task

- Execute `MIGRATION-SESSION-05-GATE` from `TASK.md`; diagnose the isolated/full-suite audit-test difference and clear the 98-test gate before activating Session 06.
