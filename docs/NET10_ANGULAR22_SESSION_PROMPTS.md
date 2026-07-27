# DBS POS Admin Tool — Session-by-Session Implementation Prompts

> Implementation instructions for controlled coding sessions. Nothing in this document has been
> executed. Revised 2026-07-26 after review; see §14.1 of the migration plan for what changed.
>
> Authority: `docs/NET10_ANGULAR22_MIGRATION_PLAN.md`. Where this runbook and the plan disagree,
> the plan wins — stop and reconcile them rather than guessing.

## How to use this runbook

1. Complete sessions **in order**, one per working session. There are 15 (00–14).
2. Start each session in a clean, current workspace with no uncommitted changes.
3. Give the coding agent the **shared preamble plus exactly one session prompt**. Nothing else.
4. Never ask a session to "continue as far as possible." Stop at its stated boundary.
5. Review the diff, the verification output, the risks, and the session log before starting the next.
6. If a session needs to change an approved decision, update the ADR and the migration plan **in
   that same session**, and say so in the handoff.
7. Sessions marked *(security judgment)* — **03, 09, 11** — carry the highest risk of a subtle
   exploitable mistake. Use the strongest available reasoning and review them hardest. Sessions 01
   and 13 are the most mechanical.
8. **Stop at the Session 07 gate** and make an explicit go/no-go decision before continuing.

### Environment facts an implementing agent should not have to rediscover

- Shell is **PowerShell 5.1** on Windows 11. `&&` and `||` are not available; use `;` or
  `if ($?) { ... }`. `curl` is an alias for `Invoke-WebRequest` — call `curl.exe` explicitly.
- Solution: `PosAdminTool.sln` — 4 source projects, 3 test projects, 14 existing test methods.
- `Directory.Build.props` currently pins `LangVersion 13.0`. There is no `global.json` yet.
- **WinUI must be published, not merely built, to run.** See `run_app.cmd`: it runs
  `dotnet publish ... -c Debug -r win-x64 --self-contained false` and sets
  `POS_ADMIN_SKIP_ELEVATION=true`. A green `dotnet build` does not prove the parity baseline works.
- Agent loopback port for all examples below: **5001**. Fix the real value in Session 01 and record
  it in the session log.

## Shared preamble for every session

Copy this block verbatim before the selected session prompt. It is mirrored in
`excute_prompt.md`; if you edit one, edit both.

```text
You are implementing one controlled session of the DBS POS Admin Tool migration.

Repository authority:
- Read docs/NET10_ANGULAR22_MIGRATION_PLAN.md completely before editing anything.
- Read docs/NET10_ANGULAR22_SESSION_PROMPTS.md and follow ONLY the assigned session.
- Read all ADRs under docs/adr/ and docs/migration/SESSION_LOG.md if they exist.
- Run `git status` and inspect the working tree before editing. Preserve user changes and never
  overwrite unrelated work.

Context you can rely on:
- The repository already targets .NET 10. This is a WinUI-to-Angular/ASP.NET Core presentation
  migration, not a runtime upgrade.
- The driver is UI modernization (plan section 0.1). The tool being replaced is ~4,200 lines of C#
  across 61 files. Keep every addition proportionate to that.
- Section 0.3 of the plan lists features deliberately CUT with justifications. Do not reintroduce
  them. There is no SQLite, no SignalR, no PWA, no service worker, no IndexedDB, no LAN mode, no
  device pairing, and no role matrix.
- Section 13 of the plan lists decisions already made. Do not re-open or silently reinterpret one.
  Exactly two remain open: Windows Service identity, and C# 14 versus C# 13.

Core constraints:
- Keep PosAdminTool.WinUI buildable and runnable until Session 14 and until parity is explicitly
  approved. Verify it by PUBLISHING it (see run_app.cmd), not by building it.
- Angular never performs Windows service, SQL Server, SMB, privileged file, or cleanup operations
  directly. Those belong to the .NET Windows agent.
- The agent binds to 127.0.0.1 only. There is no configuration path to a non-loopback bind.
- Local operation must require no internet. Bundle runtime assets, fonts, and icons locally. No CDN
  and no cloud-runtime dependency.
- Never expose, log, return, cache, or commit credentials, tokens, connection strings, private keys,
  or raw sensitive exception text.
- Never add a hard-coded password or an environment-specific server address.
- Never accept a raw file-system path, absolute path, service name, or UNC path from an HTTP
  request. Use server-issued opaque IDs and the allowlisted browse handles in plan section 5.7.
- Destructive operations require server-side policy, authorization, a preview, an expiring one-time
  challenge, typed confirmation, policy recomputed at execute time, an audit record, and tests.
- Never queue a destructive action for replay after a disconnect. Recovery is read-only.
- Use exact dependency versions and commit lockfiles. No wildcards, no ranges.
- Use UTC for persisted timestamps and opaque IDs in public contracts.
- Keep API contracts under /api/v1 and return RFC 9457 Problem Details for API errors.
- Maintain keyboard accessibility, visible focus, WCAG 2.2 AA contrast, reduced-motion support, and
  responsive behavior down to 360 px.
- Do not weaken tests, analyzers, validation, authentication, or safety controls to make a check
  pass. If a check fails, fix the cause or stop and report it.

Working method:
1. Restate the session scope and list the files and areas you expect to touch.
2. Inspect the relevant existing code and tests before changing anything.
3. Make the smallest cohesive implementation that satisfies this session and nothing more.
4. Add or update tests alongside the implementation, not afterwards.
5. Run this session's Verification commands and report their EXACT output, including failures. If an
   external dependency is unavailable, use the specified fake or fixture and state clearly which
   real-device check went unrun. Never describe a command as passing unless you ran it and saw it pass.
6. Run the standing regression gate below.
7. Review the diff for secrets, unsafe paths, non-loopback binding, unrelated edits, reintroduced
   cut features, and accidental WinUI damage.
8. Update docs/migration/SESSION_LOG.md with decisions, changes, verification output, risks, and the
   next session's prerequisites.
9. Commit once, at the end, on branch migration/session-NN where NN is this session number. Use a
   descriptive message. Do NOT push, do NOT open a pull request, do NOT force-push, and do NOT
   commit to main.
10. End with a concise handoff: outcome, files changed, tests run with results, unresolved risks,
    and whether the next session is unblocked.

Standing regression gate — run in EVERY session from the session that introduces each suite onward,
not only in the session that created it:
- The secret-scan tests: no test secret appears in any API response, log line, or audit record.
- The path-policy tests: traversal, absolute paths, reparse points, and protected roots are rejected.
- The loopback test: no non-loopback listener exists.
- The full existing .NET and Angular unit suites.
A later session that breaks an earlier invariant is a regression, not an acceptable trade-off.

Stop conditions:
- Stop and ask for direction if requirements conflict with an approved ADR or with the migration plan.
- Stop before ANY destructive action against a real RMS installation, SQL Server database, Windows
  service, SMB share, certificate store, firewall rule, or installed Windows Service, unless the
  user has explicitly authorized that exact environment and that exact action.
- Use only fakes, disposable fixtures, and temporary sandbox directories for verification.
- Do not publish, deploy, install to the service control manager, push, open a PR, or remove WinUI
  unless the assigned session explicitly requests it AND the user authorizes it.
```

---

## Session 00 — Baseline, parity matrix, screen map, and ADRs

```text
Goal:
Create the decision and parity baseline. Do NOT add the Agent project or the Angular workspace in
this session.

Tasks:
1. Re-audit the solution against the plan's section 3.4 findings table. That table cites specific
   files and line numbers. Verify each one still holds and correct the table where the code has
   moved. Do not take the table on trust.
2. Create docs/migration/CURRENT_STATE.md: evidence-backed feature and dependency inventory with
   file/line citations, including the measured file and line counts per project.
3. Create docs/migration/FEATURE_PARITY_MATRIX.md. One row per current feature, with: current entry
   point (file and symbol), target API endpoint, target Angular route, safety class
   (read / mutating / destructive), tests required, and cutover status. Seed the route mapping from
   section 8.6.1 of the plan and extend it to individual commands, not just pages.
4. Create docs/migration/UI_PARITY_MAP.md: for each of the 11 WinUI XAML pages, capture what it
   currently shows and does, so visual and functional parity can be judged later. Since UI
   modernization is the driver, this artifact is a release gate input, not a formality.
5. Create docs/migration/RISK_REGISTER.md from section 12 of the plan, with owner, likelihood,
   impact, mitigation, trigger, and status per row.
6. Create ADRs under docs/adr/ recording the decisions in section 13 of the plan as DECIDED
   (numbers 1 through 11), each with its justification. Then create two ADRs for the genuinely open
   items and make a recommendation in each:
   - ADR: Windows Service identity — dedicated local service account versus LocalSystem. Cover SQL
     Server login mapping, SMB outbound identity under WNetAddConnection2 in session 0, file ACLs on
     the managed roots, and log-on-as-a-service rights. This is the highest-risk open decision.
   - ADR: C# 14 versus remaining on C# 13.
7. Create docs/migration/SESSION_LOG.md and record this session.
8. Change no runtime behavior, no target framework, no dependency, and no WinUI file.

Verification commands:
  git status --porcelain
      -> only new files under docs/ ; no source file modified
  dotnet build PosAdminTool.sln -c Release
      -> succeeds, confirming the baseline compiles before anything changes
  dotnet test PosAdminTool.sln -c Release
      -> 14 tests pass; record the exact count and names in CURRENT_STATE.md

Required verification:
- Every WinUI navigation area and every command reachable from it appears in the parity matrix.
- All 14 existing tests are inventoried by name and project.
- Every section 3.4 finding is re-verified against current code, with the citation corrected if needed.
- Both open ADRs state a recommendation, not just options.

Deliverable boundary:
Documentation and ADRs only. No code, no dependencies, no project files.
```

---

## Session 01 — Deterministic toolchain and solution skeleton

```text
Goal:
A reproducible .NET 10 + Angular 22 skeleton that builds offline from pinned versions, with WinUI
still working.

Prerequisite:
Session 00 ADRs exist and the two open decisions are resolved.

Tasks:
1. Add global.json pinning the .NET 10 SDK with an explicit rollForward policy.
2. Apply the C# version ADR to Directory.Build.props deliberately. It currently says
   LangVersion 13.0.
3. Replace every wildcard NuGet version with an exact version. Known wildcards:
   Microsoft.Data.SqlClient 6.*, Microsoft.Extensions.Logging.Abstractions 10.*,
   Microsoft.Extensions.DependencyInjection 10.*, Microsoft.Extensions.Logging 10.*,
   System.ServiceProcess.ServiceController 10.*, Microsoft.WindowsAppSDK 1.8.*,
   CommunityToolkit.Mvvm 8.*. Re-grep rather than trusting this list.
4. Enable <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile> and commit every
   generated packages.lock.json so `dotnet restore --locked-mode` works.
5. Add src/PosAdminTool.Contracts, src/PosAdminTool.Agent (Web SDK), and
   tests/PosAdminTool.Agent.IntegrationTests. Add all three to PosAdminTool.sln.
6. FIRST read https://angular.dev/reference/versions and record the actual supported Node.js,
   TypeScript, and RxJS ranges for Angular 22 in the session log. Do not trust any version claim
   written elsewhere in these documents. Then create the Angular 22 workspace at
   src/PosAdminTool.Web with:
   - standalone application, routing, strict TypeScript, strict Angular template checks;
   - SCSS;
   - pinned Node and package-manager versions in package.json engines plus .nvmrc;
   - a committed package-lock.json with exact versions;
   - scripts: lint, test, build, e2e.
7. Configure the Agent to bind 127.0.0.1 only, serve the Angular production output as static files,
   and provide SPA fallback in Production. Add /health/live and /health/ready.
8. Add a development proxy from the Angular dev server to the Agent.
9. Add build orchestration so `dotnet publish` of the Agent produces the Angular build too, and so
   the published output needs no Node on the target machine.
10. Add a CI pipeline with separate stages: .NET restore/build/test, Angular lint/test/build, Agent
    integration tests. CI must verify WinUI by PUBLISHING it, not building it.
11. Implement no business endpoint and no visual design in this session.

Verification commands:
  dotnet --version
  dotnet restore PosAdminTool.sln --locked-mode
  dotnet build   PosAdminTool.sln -c Release
  dotnet test    PosAdminTool.sln -c Release
  npm --prefix src/PosAdminTool.Web ci
  npm --prefix src/PosAdminTool.Web run lint
  npm --prefix src/PosAdminTool.Web run test -- --run
  npm --prefix src/PosAdminTool.Web run build
  dotnet publish src/PosAdminTool.Agent/PosAdminTool.Agent.csproj -c Release -r win-x64 --self-contained
  # start the published agent, then in another shell:
  curl.exe -sS -i http://127.0.0.1:5001/health/live       -> HTTP 200
  curl.exe -sS -i http://127.0.0.1:5001/health/ready      -> HTTP 200
  curl.exe -sS -i http://127.0.0.1:5001/                  -> HTTP 200, Angular index.html
  curl.exe -sS -i http://127.0.0.1:5001/services          -> HTTP 200, SPA fallback to index.html
  netstat -ano | Select-String LISTENING | Select-String ":5001"
      -> 127.0.0.1:5001 ONLY. Any 0.0.0.0 or :: binding is a failure.
  # parity baseline — publish, do not just build:
  dotnet publish src/PosAdminTool.WinUI/PosAdminTool.WinUI.csproj -c Debug -r win-x64 --self-contained false
      -> succeeds; then launch the published exe with POS_ADMIN_SKIP_ELEVATION=true and confirm the
         window opens. Report whether you were able to confirm this.

Required tests:
- All 14 existing .NET tests still pass.
- An Agent integration smoke test asserts /health/live, /health/ready, and SPA fallback.
- The Angular default unit test passes.
- A test or documented check asserts no wildcard version remains anywhere.

Review checks:
- Zero wildcard dependency versions in any .csproj or package.json.
- No CDN or remote runtime asset anywhere.
- The agent listens on loopback in every non-test configuration.
- WinUI remains in the solution and still publishes and runs.

Deliverable boundary:
Toolchain, project skeleton, health endpoints, and CI. No business logic, no design system.
```

---

## Session 02 — Contracts, API conventions, auth, and host file browse

```text
Goal:
Stable public contracts and cross-cutting API behavior, before any feature endpoint exists.

Tasks:
1. Define versioned DTOs in PosAdminTool.Contracts for:
   - session and capability/version metadata;
   - redacted configuration;
   - device identity and connectivity;
   - services and service actions;
   - operation summary, detail, and event;
   - backup, restore, maintenance, and downloader commands and previews;
   - artifact metadata;
   - paged activity records;
   - file-browse entries and handles.
2. Do NOT reuse AppSettings, DbDownloaderSettings, OperationResult, BranchBackupItem, or any
   infrastructure type as an HTTP DTO. These are internal and two of them carry secrets.
3. Add validation with stable machine-readable error codes.
4. Add cross-cutting behavior: RFC 9457 Problem Details, correlation IDs, UTC JSON conventions,
   enum serialization policy, cancellation propagation, request-size limits, and safe exception
   mapping that never leaks exception internals.
5. Add authentication and authorization: Negotiate (Windows Integrated), authorizing a single
   principal — membership of the local Administrators group. Add antiforgery on every mutation and a
   strict Content Security Policy including frame-ancestors 'none'. No permissive CORS; the UI is
   same-origin.
6. Implement the host file-browse surface from plan section 5.7. This is the session's most
   important new design and it exists to prevent a blocker in Session 09:
   - browse roots come from configuration, never from the request;
   - POST /api/v1/files/browse takes a root ID plus a relative sub-path and returns entries with
     name, size, and last-modified time;
   - canonicalize every resolved path and re-check containment within its declared root AFTER
     resolution;
   - reject rather than follow reparse points, junctions, and symlinks;
   - reject unresolved environment variables and parent traversal;
   - POST /api/v1/files/handles exchanges an entry for a short-lived, single-purpose, opaque handle
     bound to the requesting principal and re-validated at use time;
   - never accept or return an absolute path in any contract.
7. Add OpenAPI generation and a pinned typed Angular client generation step.
8. Add API version/compatibility metadata so the Angular shell can detect an incompatible agent.
9. Add contract serialization snapshot tests and Agent integration tests.
10. Update the parity matrix with concrete contract and endpoint names.

Verification commands:
  dotnet build PosAdminTool.sln -c Release
  dotnet test  PosAdminTool.sln -c Release
  npm --prefix src/PosAdminTool.Web run build      -> generated client compiles under strict TS
  npm --prefix src/PosAdminTool.Web run lint

Required tests:
- Every contract serializes with the intended casing and UTC format (snapshot tests).
- No contract can carry a password, token, connection string, raw path, or UNC path — assert this,
  do not merely intend it.
- Invalid requests return consistent Problem Details with a correlation ID.
- Unauthenticated and unauthorized requests are rejected on every mutation route.
- Antiforgery rejects a cookie-authenticated mutation without a token.
- File browse rejects: parent traversal, an absolute path, a path outside the root after
  canonicalization, a junction or symlink pointing outside the root, an unresolved environment
  variable, and an unknown root ID.
- A handle is rejected when expired, when reused beyond its purpose, and when presented by a
  different principal.
- The generated Angular client compiles under strict TypeScript.

Do not:
- Implement any real privileged operation.
- Add permissive CORS.
- Expose a raw service name or file-system path where a server-issued ID would do.
```

---

## Session 03 — Secure configuration *(security judgment)*

```text
Goal:
Replace unsafe user-bound JSON secret handling with a service-owned, redacted configuration system.

Tasks:
1. Remove the hard-coded SQL password default at
   src/PosAdminTool.Domain/Models/AppSettings.cs:13 (currently "P@ssw0rd").
2. Remove the hard-coded environment-specific endpoint at
   src/PosAdminTool.Domain/Models/DbDownloaderSettings.cs:5 (currently
   http://10.10.9.181:8080/rmsmainserverApi/api/Updates/CreateDbBackupUpdate). New installations
   must have no default server address.
3. Separate non-secret settings from secret values in the domain model.
4. Implement the ADR-approved Windows secure storage for BOTH the SQL password and the RDB
   password, scoped to the SERVICE account. Note that ConfigurationService currently encrypts only
   SqlPassword (lines 114 and 119) and not DbDownloader.RdbPassword, despite README.md:37 claiming
   both — fix the code and then fix the README.
5. Store service-owned configuration under %ProgramData%\DBS\PosAdminTool with ACLs restricted to
   Administrators and the service identity. Make writes atomic: temp file, flush, replace.
6. Implement a NON-SECRET-ONLY importer for the legacy %USERPROFILE%\.pos_admin_tool\config.json:
   - read and validate; import branch code, POS number, paths, service list, server addresses, and
     known branches;
   - DO NOT attempt to decrypt or migrate the two secrets. The legacy key derivation is bound to the
     interactive user identity and the service account cannot read it. This is a decided trade-off,
     not a gap to work around — see plan section 5.5.
   - never modify, rewrite, or delete the legacy file; it stays as a fallback while WinUI is retained;
   - be idempotent and safe to rerun;
   - record migration version and result.
7. Implement first-run secret entry: detect that secrets are absent and require the operator to
   enter the SQL and RDB passwords once, with clear explanation of why.
8. Implement redacted GET/PUT semantics:
   - return hasSqlPassword and hasRdbPassword flags, never the secret;
   - omitted or blank secret field means KEEP the current secret;
   - clearing a secret is an explicit, separately authorized operation;
   - optimistic version/concurrency check on update.
9. Add structured redaction and secret-scanning tests. These join the standing regression gate.

Verification commands:
  dotnet build PosAdminTool.sln -c Release
  dotnet test  PosAdminTool.sln -c Release
  # prove no secret default survives anywhere:
  Select-String -Path src -Include *.cs -Recurse -Pattern 'P@ssw0rd|10\.10\.9\.181'
      -> no matches in src/ (matches in docs/ describing the fix are expected)

Required tests:
- Fresh defaults contain no credential and no environment-specific address.
- Both passwords round-trip through the new secure store.
- API responses, log lines, and audit records never contain a supplied test secret. Use a
  distinctive sentinel value and assert its total absence.
- Blank secret field preserves the existing secret; explicit clear removes it; neither path ever
  returns it.
- Version conflict on concurrent update is detected and rejected.
- Legacy non-secret import: succeeds, is idempotent on rerun, handles a corrupt file, handles a
  missing file, handles partial data, and leaves the original file byte-identical.
- First-run state is correctly detected when secrets are absent.
- File ACL behavior has a Windows integration test or a clearly documented manual fixture.

Stop:
Never test with real production credentials. Use sentinel values only.
```

---

## Session 04 — Job engine, SSE, and audit log

```text
Goal:
Make long operations independent of the HTTP request so they survive a browser refresh.

Scope note:
There is NO SQLite and NO SignalR (plan section 0.3). Job state is in agent memory and does not
survive agent restart — that matches the application being replaced, which loses all job state on
exit. Do not add durable job persistence, restart-resume logic, or an Interrupted state.

Tasks:
1. Implement a bounded in-memory operation registry: operation ID (opaque), type, branch snapshot,
   requesting principal, UTC requested/start/end times, state, monotonic progress, current stage,
   sanitized event messages, owned resource locks, result artifact IDs, safe error code, and
   correlation ID.
2. Implement the state machine: Queued, Running, Succeeded, PartiallySucceeded, Failed, Cancelled.
   No Interrupted state.
3. Implement named resource locks — sql, services, filesystem-cleanup, downloader — with a defined
   policy for a conflicting request (serialize or reject; pick one and test it).
4. Implement a bounded queue, per-operation cancellation, and progress stages.
5. Implement the append-only JSONL audit file under %ProgramData%\DBS\PosAdminTool\audit\ with
   restrictive ACLs. Append a record for every completed DESTRUCTIVE operation. No database.
6. Add REST endpoints: POST /api/v1/operations, GET /api/v1/operations,
   GET /api/v1/operations/{id}, POST /api/v1/operations/{id}/cancel.
7. Add GET /api/v1/events as a single Server-Sent Events stream carrying operation status, progress,
   connectivity, and service-state events with sanitized payloads only.
8. Add idempotency-key support so a retried submit does not start duplicate work.
9. Implement a fake diagnostic operation for tests and development only, never reachable in
   Production.
10. Keep business logic out of the SSE endpoint; it is a transport.

Verification commands:
  dotnet build PosAdminTool.sln -c Release
  dotnet test  PosAdminTool.sln -c Release
  # manual SSE smoke against a running agent:
  curl.exe -sS -N http://127.0.0.1:5001/api/v1/events
      -> streams text/event-stream frames while a fake operation runs

Required tests:
- Valid and invalid state transitions.
- A duplicate idempotency key does not duplicate work.
- Conflicting resource locks serialize or reject according to the documented policy.
- Queue capacity behavior when full.
- Cancellation reaches the running operation and is reflected in state.
- A client that disconnects and re-reads GET /api/v1/operations/{id} receives current state.
- An SSE disconnect does NOT cancel server work.
- Reconnecting after an SSE drop does not re-issue or duplicate a command.
- Every destructive operation produces exactly one audit record, and no audit record contains a secret.

Do not:
- Add SQLite, a durable store, or restart-resume behavior.
- Keep the only copy of operation truth in the browser.
```

---

## Session 05 — Angular design system and application shell

```text
Goal:
Implement the Branch Signal Desk visual system and responsive shell. No feature business logic.

This is the centerpiece session. UI modernization is the entire driver of this migration, so
quality here is a release gate, not a preference.

Design authority:
Follow section 8 of docs/NET10_ANGULAR22_MIGRATION_PLAN.md exactly. Do NOT substitute a generic
Angular Material dashboard, a gradient hero, a metric-card grid, or a rounded-pill-heavy theme.

Tasks:
1. Bundle Barlow Condensed, Source Sans 3, IBM Plex Mono, and the selected outline icon set
   LOCALLY. Verify each license permits redistribution and include the required notices. No CDN,
   no Google Fonts request, no emoji as iconography.
2. Implement semantic light and dark tokens from sections 8.2 and 8.3, plus the type scale, 4 px
   spacing system, radii (4 px controls, 8 px panels, 12 px major dialogs only), focus treatment,
   and elevation.
3. Build primitives: status marker, form controls, table/list, dialog, progress, skeleton, empty
   state, error state, and toast.
4. Build the shell: desktop rail, compact tablet navigation, phone bottom navigation, top device
   context strip, activity rail, agent-unreachable banner, and a global error boundary.
5. Implement the accessible branch signal path component with loading, ready, degraded, unreachable,
   stale, and unknown states. Each node must be keyboard reachable, expose its evidence and
   timestamp, and route to the relevant diagnostic area.
6. Add layout routes and placeholders for Overview, Device, Services, Backups, Restore, Maintenance,
   Downloads, Activity, and Settings.
7. Use Angular CDK primitives for overlays, focus trapping, and live regions, but keep custom DBS
   styling — do not adopt a component library's visual language.
8. Add a development-only component gallery route.
9. Use real domain vocabulary and realistic fake fixture data.
10. There is no service worker, no PWA manifest, no IndexedDB, and no update-ready notice.

Verification commands:
  npm --prefix src/PosAdminTool.Web run lint
  npm --prefix src/PosAdminTool.Web run test -- --run
  npm --prefix src/PosAdminTool.Web run build
  # asset audit — prove nothing is fetched from the internet:
  Select-String -Path src/PosAdminTool.Web/dist -Include *.js,*.css,*.html -Recurse -Pattern 'https?://(?!127\.0\.0\.1|localhost)'
      -> no matches. Any external URL in the production bundle is a failure.
  npm --prefix src/PosAdminTool.Web run e2e
      -> accessibility and keyboard specs pass

Required tests:
- Component unit tests for status semantics and navigation state.
- Keyboard traversal and focus-order tests for the shell and every dialog.
- Automated accessibility checks (axe or equivalent) on the shell and dialogs, in light AND dark.
- Reduced-motion behavior: all status remains comprehensible with motion removed.
- Contrast assertions for every semantic token pair against WCAG 2.2 AA.
- Status is never conveyed by color alone.
- The production build issues zero external font, icon, image, or script requests.

Deferred to Session 13 deliberately:
Responsive visual snapshot baselines. Fixture data shapes will change in Sessions 06 and 07, so
snapshots taken now would churn. Do not create them yet.

Self-critique before handoff (required, and record it in the session log):
- Name any element that still resembles a generic admin template, and revise it.
- Confirm the signal path is the single memorable visual and that surrounding UI is restrained.
- Compare against docs/migration/UI_PARITY_MAP.md and state plainly whether this is better than the
  WinUI pages it replaces, and why.
```

---

## Session 06 — Device overview and configuration

```text
Goal:
Overview, Device, and Settings parity backed by real agent data.

Tasks:
1. Implement endpoints for device identity, capabilities, local and main-server connectivity,
   configuration read/update, RMS import, branch verification, and database connection test.
2. ADAPT the existing use cases rather than duplicating them: ImportFromRmsUseCase,
   TestConnectionUseCase, BranchVerificationService, ConnectivityMonitor, and the configuration
   logic. Read them first; refactor host and file-system dependencies behind ports.
3. VALIDATE THE SERVICE IDENTITY AGAINST SQL SERVER EARLY IN THIS SESSION. The current app connects
   as the elevated interactive technician; the agent will not. If the chosen service account cannot
   authenticate to the branch database, that is a blocking finding — report it before building UI on
   top of it. Do not paper over it with a fake adapter and move on.
4. Return redacted data plus safe diagnostic evidence with last-checked timestamps.
5. Implement Overview signal-path binding, active-operation summary, one recommended corrective
   action when something is unhealthy, and recent activity.
6. Implement Device details and Settings forms, including the browse-roots configuration.
7. Add typed validation, dirty-form protection, version-conflict handling, secret
   keep/replace/clear UX, and fresh/stale/unknown states with last-checked times.
8. Never hold a secret in Angular state longer than the submission requires, and never persist one.

Verification commands:
  dotnet build PosAdminTool.sln -c Release
  dotnet test  PosAdminTool.sln -c Release
  npm --prefix src/PosAdminTool.Web run test -- --run
  npm --prefix src/PosAdminTool.Web run build
  npm --prefix src/PosAdminTool.Web run e2e -- --grep "configuration"

Required tests:
- Authorization and validation on every new endpoint.
- Import and update never overwrite a retained secret by accident.
- A version conflict preserves the user's unsaved values rather than discarding them.
- The signal path derives correct healthy, degraded, and unreachable states from evidence.
- E2E: load imported legacy config, edit it, test the DB connection via a fake adapter, verify the
  branch, save, reload, and confirm no secret was returned at any point.

Do not:
- Return a decrypted secret under any circumstance.
- Claim that TCP reachability means an application-level health check succeeded. Label evidence
  accurately — the current app conflates "agent cannot be reached" with "main RMS server cannot be
  reached", and the new UI must distinguish them.
```

---

## Session 07 — Windows service management → GO / NO-GO GATE

```text
Goal:
Services parity through the agent, then an explicit project gate.

Tasks:
1. Adapt WindowsServiceManager (src/PosAdminTool.Infrastructure/Windows/WindowsServiceManager.cs)
   behind application commands and authorized API endpoints. It currently uses ServiceController and
   shells out to sc.exe at line 118.
2. Expose server-issued service IDs and display metadata. NEVER accept an arbitrary service name
   from a request — that would let a caller control any service on the machine.
3. Implement bulk status reads, last-checked times, transition states, timeouts, a cancellation
   policy, per-service locks, idempotency, and audit.
4. Build the responsive Services UI with distinct command-sent, running, confirmed, and failed
   states. Indicate "command sent" optimistically but never claim success before the agent confirms it.
5. Disable conflicting commands while a service transition is in flight.
6. Rehydrate service state after a browser refresh and after an SSE drop.
7. Replace the WinUI DispatcherQueueTimer polling model with server-side polling plus SSE.

Verification commands:
  dotnet build PosAdminTool.sln -c Release
  dotnet test  PosAdminTool.sln -c Release
  npm --prefix src/PosAdminTool.Web run test -- --run
  npm --prefix src/PosAdminTool.Web run e2e -- --grep "service"

Required tests:
- A fake adapter covers Running, Stopped, NotFound, Unknown, timeout, access denied, and command
  failure.
- An integration fixture verifies endpoint authorization and lock conflicts.
- A request naming a service that is not in the configured list is rejected.
- E2E: refresh, start, stop, restart, double-click prevention, and disconnect/reconnect.
- Accessibility: every action's accessible name includes the service context, and status is not
  color-only.
- A real-Windows fixture is opt-in and targets ONLY a disposable test service.

Stop:
Never control an actual RMS or system service without explicit environment authorization from the user.

=== GO / NO-GO GATE — do not start Session 08 without an explicit decision ===

At this point roughly a third of the effort is spent and configuration plus service control work in
a browser against the real agent. Produce docs/migration/GATE_07.md answering, with evidence:

1. Does the chosen Windows Service identity actually work for SQL Server and Windows service
   control on a representative device? Cite what you observed, not what you expect.
2. Is the Angular UI genuinely better to use than the WinUI pages it replaces? UI modernization is
   the whole point of this project (plan section 0.1); a "no" invalidates it regardless of backend
   quality. Compare against docs/migration/UI_PARITY_MAP.md.
3. Is the remaining scope still credible against the ~4,200-line baseline in plan section 0.2?
4. Which risks in the register have materialized, and which have been retired?

Then STOP and hand the decision to the user. If the answer is no, keeping WinUI and documenting the
blocker is a good outcome at one third of the budget — far better than discovering the same problem
at Session 14. Do not proceed on your own judgment.
```

---

## Session 08 — Local backup workflow

```text
Goal:
Move local backup into the operation engine and deliver a safe browser workflow.

Prerequisite:
The Session 07 gate returned GO.

Tasks:
1. Refactor BackupService (src/PosAdminTool.Application/Services/BackupService.cs) so file-system
   and host-shell behavior sits behind ports.
2. Preserve the existing selectable set: branch DB, cashier DB, and the three appsettings files.
3. Choose the destination via a browse handle from plan section 5.7 — never a free-text path.
4. Validate the managed destination, free disk space, selected components, branch identity,
   database identifiers, and configuration sources before starting.
5. Create a versioned archive manifest with branch, POS, release, UTC creation time, contents,
   sizes, and checksums, while keeping existing archives readable and archive names human-readable.
6. Persist job progress and artifact metadata in the operation registry. Expose downloads by
   artifact ID with a safe Content-Disposition. Stream; never buffer a database archive in memory.
7. Implement the select / review / run / progress / result / catalog UI. Keep the selected branch
   and target database visible at the review step.
8. Do NOT open Explorer from server-side logic. BackupService.cs:276 currently does this via
   Process.Start. Replace the affordance in the UI: show the resolved destination path in mono type
   with copy-to-clipboard, plus a direct artifact download. Losing this silently is a parity
   regression — see plan section 8.7.

Verification commands:
  dotnet build PosAdminTool.sln -c Release
  dotnet test  PosAdminTool.sln -c Release
  npm --prefix src/PosAdminTool.Web run test -- --run
  npm --prefix src/PosAdminTool.Web run e2e -- --grep "backup"

Required tests:
- Selection mapping and safe archive naming.
- SQL compatibility retry behavior is preserved.
- Missing config file, missing DB backup, partial failure, cancellation, disk full, and staging
  cleanup on every exit path.
- Artifact download authorization and streaming behavior.
- A destination handle outside the allowlisted roots is rejected.
- E2E: run a backup against fake SQL and file adapters, refresh the browser mid-progress, confirm
  progress is recovered, then download the result.

Stop:
Do not execute BACKUP DATABASE against a real database without explicit authorization.
```

---

## Session 09 — Restore backend and archive hardening *(security judgment)*

```text
Goal:
Server-side restore capability with full archive defenses and a mandatory preview. Backend only —
the UI is Session 10.

Tasks:
1. Add two source mechanisms and keep them clearly distinct: a streamed bounded upload, and
   selection of a file already on the device via a browse handle (plan section 5.7). The second is
   the correct path for a multi-gigabyte .bak; uploading one through the browser to the machine it
   already sits on is not acceptable.
2. Validate archives before extracting anything: entry paths, entry count, total expanded bytes,
   compression ratio, permitted extensions, duplicate names, manifest and checksums, branch
   mismatch, and destination mappings. Reject absolute paths, parent traversal, and reparse points.
3. Build the restore preview: target database, logical SQL files, MOVE destinations, config
   overwrites, services affected, required free space, and warnings.
4. Require a short-lived one-use server challenge plus typed confirmation for overwrite execution.
   Recompute all policy at execute time. A stale, reused, or expired challenge fails closed.
5. Add resource locks, durable stages within the operation registry, a cancellation policy, audit
   records, and post-restore verification.
6. Support full, database-only, and config-only modes at the API level.

Verification commands:
  dotnet build PosAdminTool.sln -c Release
  dotnet test  PosAdminTool.sln -c Release

Required tests:
- Valid old-format and new-format archives both restore.
- Every abuse case is rejected: path traversal, absolute paths, junction/symlink escape, ZIP bomb by
  ratio, excessive entry count, excessive expanded size, checksum mismatch, duplicate entry names,
  multiple ambiguous .bak files, wrong branch, and unknown JSON files.
- SQL logical file mapping is correct; restore failure, config-copy failure, mid-operation
  interruption, and post-check failure are all handled.
- A stale, reused, or expired preview challenge fails closed.
- Upload size limits are enforced and a rejected upload does not leave staging files behind.

Stop:
Never restore a real database or overwrite real RMS files. Use disposable fakes and temporary
directories only.
```

---

## Session 10 — Restore UI flows

```text
Goal:
The browser-side restore experience for all three modes.

Prerequisite:
Session 09 is complete and its tests pass.

Tasks:
1. Implement source selection with the two mechanisms visually distinct: upload from this browser,
   or pick a file on this device via the browse picker. Never present a free-text host-path box.
2. Implement archive inspection display, showing what the server found rather than what the client
   guessed.
3. Implement the review step: target database, logical files, destinations, services that will be
   stopped, free-space requirement, and warnings. Keep the branch and target database visible.
4. Implement the confirmation step: typed confirmation with no preselected acceptance, correct focus
   management on open and close, and clear expiry behavior when the challenge goes stale.
5. Implement progress and result for full, database-only, and config-only modes.
6. Never let an operation outcome exist only in a transient toast.

Verification commands:
  npm --prefix src/PosAdminTool.Web run lint
  npm --prefix src/PosAdminTool.Web run test -- --run
  npm --prefix src/PosAdminTool.Web run build
  npm --prefix src/PosAdminTool.Web run e2e -- --grep "restore"

Required tests:
- All three restore modes end to end against fakes.
- A stale preview is surfaced honestly in the UI and cannot be submitted.
- Typed confirmation is required; the submit control stays disabled until the phrase matches.
- Focus moves correctly into and out of the confirmation dialog, and focus is never trapped in the
  non-modal activity panel.
- Accessibility checks pass on every new dialog in light and dark.
- Browser refresh mid-restore recovers progress read-only and never re-issues the command.
```

---

## Session 11 — Cleanup and branch reset safety *(security judgment)*

```text
Goal:
Replace a client-only checkbox with enforceable server-side maintenance safety.

This session fixes a real defect. CleanupService.cs lines 31-48 currently call
Environment.ExpandEnvironmentVariables and then Directory.Delete(recursive: true) on configured
paths with no allowlist and no protected-root check. A configured value of "C:\" would be honored.
Read that file before writing anything.

Tasks:
1. Implement canonical path policy: managed roots, a protected-root denylist, environment-variable
   resolution checks, UNC policy, reparse-point/junction/symlink checks, and separation of install
   from data directories. Canonicalize FIRST, then check containment.
2. Build a cleanup preview identifying the exact services and paths, whether each exists, estimated
   item count and size where practical, and the reason for every policy rejection.
3. Build a reset preview identifying the branch, database, and affected tables and record counts
   where safely queryable.
4. Require: the authorized principal, a fresh one-time challenge, typed branch-code or phrase
   confirmation, idempotency, resource locks, and an immutable audit record.
5. Recompute ALL policy immediately before execution. Never trust the preview's conclusions.
6. Implement a dedicated Maintenance route, visually and structurally separate from routine Backup
   and Restore actions. Destructive controls must not sit beside routine ones.
7. Show the exact services, paths, tables, and branch affected. Add recovery guidance and sanitized
   diagnostic output.

Verification commands:
  dotnet build PosAdminTool.sln -c Release
  dotnet test  PosAdminTool.sln -c Release
  npm --prefix src/PosAdminTool.Web run test -- --run
  npm --prefix src/PosAdminTool.Web run e2e -- --grep "cleanup"

Required tests:
Reject every one of these, each as its own test case:
  drive root (C:\), C:\Windows, C:\Program Files, ProgramData root, user profile root, the
  application install root, the application data root, parent traversal (..), an unresolved
  environment variable, an unapproved UNC path, a junction or symlink escaping a managed root, and a
  duplicate, stale, expired, or reused challenge.
Also:
- A preview/execute mismatch fails closed.
- Partial service-stop and partial deletion outcomes are reported accurately, not as success.
- A client cannot execute by forging checkbox or confirmation state — assert this directly against
  the API, bypassing the UI.
- Every executed cleanup and reset produces an audit record containing no secret.
- E2E runs ONLY against temporary sandbox directories with fake SQL and service adapters.

Stop:
Do not delete any real project, user, RMS, Windows, or database data during verification. Create a
throwaway directory tree for every deletion test and assert it was the only thing touched.
```

---

## Session 12 — DB Downloader

```text
Goal:
Main-server backup triggering, durable observation, and safe result download, without exposing SMB
details to the browser.

Tasks:
1. Adapt DbDownloadService, BackupApiClient, SmbBackupRepository, and SmbPathResolver into the
   operation model. Read the 5 existing DbDownloadService tests first and preserve every one.
2. Preserve exactly: one batch trigger for the selected branches, newest-created-folder discovery,
   exact branch ZIP matching, stable-size-across-observations validation, and independent per-branch
   progress and timeout.
3. Replace cancellation-insensitive delays with cancellation-aware waits.
4. Validate the API scheme, host, and port against policy; validate the SMB target, root mapping,
   branch-code syntax, and interval and timeout bounds. Defend against SSRF and unsafe schemes.
5. Use the encrypted RDB credential server-side only. Expose artifact IDs in UI contracts — never a
   UNC path, never a credential.
6. VALIDATE SMB UNDER THE REAL SERVICE IDENTITY. WNetAddConnection2
   (src/PosAdminTool.Infrastructure/Smb/SmbConnectionScope.cs:66) maps a connection into the
   caller's logon session, and a Windows Service in session 0 behaves differently from the current
   interactive elevated process. This is a known trap and a recorded risk (plan section 3.4 item
   14). Prove it works or report it as a blocking finding. Do not assume.
7. Never duplicate the main-server trigger.
8. Implement the branch catalog and settings, selected branches, batch timeline, per-branch state,
   download, cancellation, and retry UI.

Verification commands:
  dotnet build PosAdminTool.sln -c Release
  dotnet test  PosAdminTool.sln -c Release
  npm --prefix src/PosAdminTool.Web run test -- --run
  npm --prefix src/PosAdminTool.Web run build

Required tests:
- All 5 existing DbDownloadService tests still pass unchanged in intent.
- New cases: cancellation mid-observation, ambiguous folders, clock skew, file disappearance, size
  changing between observations, duplicate branch in one batch, unsafe URL, unsafe SMB path,
  authentication failure, and download interruption.
- No API response, log line, audit record, or UI payload contains the RDB password or a UNC
  administrative share path — assert with a sentinel value.
- Multiple branches progress independently; one branch failing does not fail the batch.

Stop:
Do not call the real backup endpoint or connect to the real SMB server without explicit
authorization for that exact environment.
```

---

## Session 13 — UI polish, accessibility, and release hardening

```text
Goal:
Close the quality, accessibility, and observability gaps before packaging.

Tasks:
1. Reconcile every parity-matrix row and every release gate in section 11 of the plan.
2. Complete the unit, Agent integration, Windows adapter fixture, Angular, Playwright, and
   accessibility suites.
3. Create the responsive visual snapshot baselines deferred from Session 05: 360, 768, 1280, and
   1600 px, in light and dark. Real data shapes are settled now, so these will be stable.
4. Add concurrency, cancellation, and large-archive streaming tests. Add disk-pressure and
   log-retention tests.
5. Add sanitized diagnostic export with correlation IDs, plus explicit redaction tests.
6. Add structured log retention, audit-file rotation, health diagnostics, and clock-skew handling.
7. Add a dependency and license inventory covering the three bundled font families and the icon
   set. (No SBOM and no vulnerability scanner in v1 — plan section 0.3.)
8. Measure Angular bundle budgets, startup time, memory, API latency, and SSE reconnect behavior.
9. Perform a manual UX critique at desktop, tablet, and phone widths, in light and dark, with
   keyboard only, with a screen reader, and with reduced motion. Record findings and fix them.
10. Compare the finished UI against docs/migration/UI_PARITY_MAP.md and state plainly whether the
    driver has been met.
11. Update the support and operator runbooks.

Verification commands:
  dotnet build PosAdminTool.sln -c Release
  dotnet test  PosAdminTool.sln -c Release
  npm --prefix src/PosAdminTool.Web run lint
  npm --prefix src/PosAdminTool.Web run test -- --run
  npm --prefix src/PosAdminTool.Web run build
  npm --prefix src/PosAdminTool.Web run e2e
  Select-String -Path src/PosAdminTool.Web/dist -Include *.js,*.css,*.html -Recurse -Pattern 'https?://(?!127\.0\.0\.1|localhost)'
      -> no matches
  netstat -ano | Select-String LISTENING | Select-String ":5001"
      -> 127.0.0.1 only

Required output:
- docs/migration/RELEASE_READINESS.md listing every gate as Pass, Fail, Blocked, or Accepted
  Exception, each with evidence.
- No accepted exception may conceal a credential exposure, a destructive-safety gap, an
  authorization gap, a data-loss risk, a non-loopback listener, or a critical accessibility failure.

Do not:
- Package the installer or remove WinUI in this session.
```

---

## Session 14 — Offline installer and cutover

```text
Goal:
Produce the deployment package, validate upgrade and rollback, pilot safely, and remove WinUI only
after explicit approval.

Prerequisites:
- RELEASE_READINESS.md has no blocking failure.
- The user explicitly authorizes installer and pilot work.
- Disposable, non-production test devices are identified.

Tasks:
1. Build a signed self-contained win-x64 installer containing the Agent and .NET runtime, the
   Angular production build, the local fonts, icons, and help content, service configuration, and
   ACL setup. No LAN certificate or firewall components — LAN mode is not in v1.
2. Implement install, repair, upgrade, rollback, and uninstall flows.
3. Preserve or remove configuration, audit, and backup data on uninstall only through an explicit
   user choice.
4. Test a clean install with network adapters disconnected.
5. Test upgrade from the pilot version with configuration, audit, and artifact preservation.
6. Test a failed upgrade rolling back cleanly.
7. Run the complete parity matrix on representative non-production devices.
8. Collect and record operator acceptance.
9. ONLY after explicit user approval:
   - remove src/PosAdminTool.WinUI;
   - remove the Windows App SDK dependency and the XAML publish workarounds;
   - remove run_app.cmd;
   - update PosAdminTool.sln, README.md, and the build scripts;
   - make WinUI removal a DEDICATED, easily reviewable commit containing nothing else.
10. Produce final architecture, deployment, support, backup/restore, and disaster-recovery
    documentation.

Verification commands:
  dotnet build PosAdminTool.sln -c Release
  dotnet test  PosAdminTool.sln -c Release
  npm --prefix src/PosAdminTool.Web run e2e
  Get-FileHash <installer> -Algorithm SHA256
  netstat -ano | Select-String LISTENING | Select-String ":5001"
      -> 127.0.0.1 only, on the installed service

Required verification:
- Installer hash and signature recorded.
- Offline clean install, launch, local use, service restart, and uninstall all evidenced.
- Upgrade and rollback evidenced.
- Loopback-only listener evidenced on the installed service.
- No secret in any installer log or diagnostic bundle.
- Full test suite and complete parity matrix run.

Stop:
- Do not deploy to production, and do not remove WinUI, without explicit user authorization.
- If any parity or safety gate fails, RETAIN WinUI and document the blocker.
```

---

## Final implementation handoff template

Use this after Session 14:

```text
Migration outcome:

Version/toolchain:
- .NET SDK:
- C# version (and why):
- Angular:
- Node:
- Package manager:
- Installer version:

Supported mode:
- Local loopback only: yes/no
- Architecture: win-x64

Parity:
- Rows passed:
- Accepted differences:
- Remaining blockers:

UI modernization — the project driver:
- WinUI pages replaced (per UI_PARITY_MAP.md):
- Signal path implemented:
- WCAG 2.2 AA result, light and dark:
- Manual keyboard and screen-reader result:
- Honest assessment: is this better than what it replaced, and on what evidence?

Security:
- Service identity chosen, and what it can/cannot do:
- Secret storage mechanism:
- Authentication:
- Destructive-operation controls:
- Loopback-only evidence:

Verification:
- .NET tests (count, result):
- Angular tests (count, result):
- Agent integration tests:
- Playwright journeys:
- Accessibility:
- Offline install:
- Upgrade/rollback:
- Secret scan:

Deployment artifacts:
- Installer:
- SHA-256:
- License notices for bundled fonts/icons:
- Release notes:

Operations:
- Config/data location:
- Backup/artifact location:
- Audit file location and rotation:
- Support runbook:
- Rollback procedure:

WinUI status:
- Retained or removed:
- Approval and evidence:

Deferred to a future increment (from plan section 0.3):
- LAN mode with pairing and roles:
- Durable job persistence:
- Offline/PWA behavior:
```
