# Current Project State

- **Updated:** 2026-07-29
- **Branch:** `migration/session-04`
- **Baseline commit:** `1d230bf0e0af55b96d1ce1c4f39992d47a05b6a3`
- **Release or milestone:** .NET 10 + Angular 22 migration; Sessions 00-04 complete, Session 05 ready

## Working State

- The retained WinUI application remains the functional parity baseline for configuration, service control, backup/restore, maintenance, downloader, and activity workflows.
- The migration solution now includes Domain, Application, Infrastructure, retained WinUI, Contracts, Agent, Web, and four xUnit test projects.
- The Agent implements loopback hosting, Negotiate/admin authorization, antiforgery, safe Problem Details/correlation IDs, session discovery, and allowlisted file browsing with opaque handles.
- Agent-owned configuration, machine-scope DPAPI secrets, optimistic versioning, secret keep/replace/clear behavior, and idempotent non-secret legacy import are implemented.
- A bounded in-memory operation engine, principal-scoped idempotency, named locks, cancellation, REST rehydration, SSE, and destructive JSONL audit are implemented; only Development fake operations use it so far.
- The Angular workspace has pinned tooling and generated-client plumbing, but its UI remains the starter placeholder with no application routes; Session 05 is the active next task.
- The latest recorded application validation is Session 04: Release solution build passed with zero warnings/errors and 98/98 tests passed. These checks were not re-run for context initialization.
- CI defines locked .NET restore/build/test, Angular lint/test/build, Agent integration tests, and retained WinUI publish on Windows runners.
- The planned Windows Service installer and real operational Agent endpoints/screens are not implemented.

## Active Blockers

- None confirmed.

## Known Risks

- SQL, managed-root ACL, and SMB access under the proposed dedicated Windows Service identity require representative-device proof; SMB Session 0 behavior is the highest environment risk.
- Manual live-Agent SSE smoke and a real browser Negotiate/admin round trip have not been recorded.
- Legacy WinUI cleanup and restore retain documented destructive-path and archive-validation weaknesses until Sessions 09 and 11 replace those flows safely.
- Angular UI modernization, accessibility, and parity remain unproven; the explicit go/no-go evaluation occurs after Session 07.

## Recently Completed

- Session 04 added the Agent operation registry, resource locks, SSE progress, idempotency, cancellation, and destructive-operation audit.
- Session 03 added secure Agent configuration, DPAPI secret storage, legacy non-secret import, and configuration API coverage.
- Session 02 added versioned contracts, API conventions, Windows/admin authorization, antiforgery, safe file browsing, and generated-client plumbing.
- Session 01 pinned the toolchains/dependencies and added the Agent, Angular, Contracts, integration-test, and CI skeleton.

## Next Recommended Task

- Execute `MIGRATION-SESSION-05` from `TASK.md`: implement and validate the Angular Branch Signal Desk design system and responsive shell without feature business logic.
