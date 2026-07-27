# Current State

## Snapshot

- Last updated: 2026-07-27
- Updated by: Sol 5.6 High
- Current branch: `migration/session-02`
- Repository status: Only the three tracked `.ai` project-memory files are modified; no production-code changes.
- Overall project status: Retained WinUI application implemented; Agent/Angular replacement partially implemented through migration Session 02.
- Active workstream: Project-memory initialization; migration Session 03 is next.
- Confidence level: High for source, configuration, tests, Git, and migration evidence; medium for live external integrations.

## Current Status

The retained WinUI application contains configuration, Windows service control, backup/restore, cleanup/reset, DB Downloader, and logging workflows. Its Domain, Application, Infrastructure, and UI layers remain in the solution as the parity baseline.

Migration Sessions 00-02 are committed. The deterministic .NET/Angular toolchain, Agent host, API contracts, Windows authentication/authorization conventions, antiforgery, Problem Details/correlation behavior, OpenAPI client generation, and allowlisted file-browse handles exist. Operational Agent endpoints and Angular feature screens have not started.

## Module Status

| Module | Status | Test Status | Notes |
|---|---|---|---|
| Domain | Implemented | Tested | 4 current xUnit cases; coverage is limited. |
| Application | Implemented | Tested | 8 current xUnit cases; core retained workflows have partial coverage. |
| Infrastructure | Implemented | Tested | 5 current xUnit cases; no live external-system tests. |
| WinUI | Implemented | Tested | Published/launched in Session 01; no automated UI suite. |
| Contracts | Tested | Tested | Shape and serialization rules covered by Agent integration tests. |
| Agent | Partially Implemented | Tested | Host/API foundation and file browsing only; 37 integration tests. |
| Angular Web | Partially Implemented | Tested | Scaffold shell, empty routes, generated client; 2 unit tests. |
| CI | Implemented but Not Tested | Unknown | Workflow exists; remote run status was not inspected. |
| Installer/Windows Service | Not Started | Not Started | Accepted target architecture only. |

## Recent Relevant Changes

- Latest documentation commit added repository instructions, the secure-configuration migration plan/prompts, and empty tracked project-memory placeholders.
- Session 02 added versioned public DTOs without Domain-model reuse.
- Session 02 added Negotiate authentication and local-administrator authorization.
- Session 02 added antiforgery, CSP/frame protection, correlation IDs, Problem Details, and JSON conventions.
- Session 02 added allowlisted file browsing and principal/purpose-bound, single-use expiring handles.
- Session 02 wired OpenAPI generation to a strict Angular typed-client build.
- Session 01 pinned the .NET/Node/npm/dependency toolchain and committed lockfiles.
- Session 01 added the Agent, Contracts, Angular, integration-test projects, CI, loopback host, and SPA publish pipeline.

## Current Uncommitted Changes

- Modified: `.ai/CONTEXT.md`, `.ai/CURRENT_STATE.md`, and `.ai/DECISIONS.md` for this initialization.
- No staged, untracked, or production-code changes detected.

## Validation Status

| Validation | Result | Notes |
|---|---|---|
| Build | Passed | `dotnet build PosAdminTool.sln -c Release --nologo`; 0 warnings, 0 errors. |
| Compilation | Passed | All 10 solution projects compiled; Agent OpenAPI document generated. |
| Unit tests | Passed | 54/54 .NET cases and 2/2 Angular cases passed. |
| Integration tests | Passed | Agent integration project: 37/37 passed. |
| End-to-end tests | Not Run | Playwright contains only a placeholder toolchain test. |
| Type checking | Passed | Angular production build passed strict TypeScript/template compilation. |
| Lint | Passed | Angular lint passed. |

## Current Blockers

- None confirmed for starting Session 03.

## Known Risks

- The proposed dedicated service identity has not been proven against SQL Server, managed-root ACLs, or SMB in Session 0.
- The retained defaults include a hard-coded non-empty SQL credential (`<REDACTED>`) and an environment-specific HTTP endpoint (`<REDACTED>`).
- The retained configuration path encrypts the SQL secret but not the RDB secret.
- Retained cleanup recursively deletes expanded configured paths without the planned server policy/preview controls.
- Retained restore extracts archives without the planned traversal, size, ratio, entry-count, or file-type protections.
- A real browser/SSPI Negotiate round trip for local-admin and non-admin principals is unverified.
- File-browse roots are empty by default until later feature sessions configure managed roots.
- UI modernization, operational parity, accessibility, installer, and rollback evidence remain future work.

## Known Defects or Gaps

- Agent APIs currently implement only session, antiforgery, file browse, and file-handle behavior; other DTOs are future contracts.
- Angular routes are empty and the visible content is scaffold-level.
- Secret-scan tests and secure Agent configuration are deferred to Session 03.
- No Agent job engine, SSE operation stream, audit log, or operational adapters are wired.
- `docs/migration/CURRENT_STATE.md` is the historical Session 00 baseline and does not describe the post-Session-02 tree; `docs/migration/SESSION_LOG.md` contains the newer migration evidence.

## Current Priorities

1. Execute Session 03 secure configuration and introduce the standing secret-scan gate.
2. Prove real Negotiate behavior and the chosen service identity on a representative Windows device before the Session 07 gate.
3. Preserve WinUI parity and current safety/build regressions while adding Agent features session by session.

## Next Recommended Task

- Objective: Implement migration Session 03 secure Agent configuration, remove unsafe/environment-specific fresh defaults, securely store both secrets, and import only non-secret legacy settings.
- Relevant module: Agent configuration with Domain/Infrastructure migration support and existing Contracts DTOs.
- Likely files: `src/PosAdminTool.Agent`, `src/PosAdminTool.Infrastructure/Configuration`, `src/PosAdminTool.Domain/Models`, `src/PosAdminTool.Contracts/V1/Configuration`, and targeted test projects.
- Expected validation: Targeted secret/configuration tests, secret scan, full .NET build/tests, Angular typed-client build/tests/lint, and current loopback/path-policy regression tests.
- Definition of done: Fresh state contains no credential or environment-specific address; SQL and RDB secrets round-trip in service-owned storage and never appear in responses/logs; non-secret legacy import is idempotent and leaves the legacy file unchanged.
