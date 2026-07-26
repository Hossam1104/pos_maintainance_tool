# Migration session log

## Session 00 — Baseline, parity matrix, screen map, and ADRs

Date: 2026-07-26
Scope: Documentation and ADRs only; no runtime, dependency, target-framework, project, or WinUI file change.

### Decisions and changes

- Re-measured the source baseline: 61 C# files, 4,198 C# lines, and 11 WinUI XAML artifacts.
- Re-verified all 15 findings from plan section 3.4 and corrected citations in `CURRENT_STATE.md`.
- Added a command-level parity matrix, all-XAML UI map, and risk register.
- Recorded the 11 already-decided plan decisions and two open-decision recommendations as ADRs `001`–`013`.
- Recommended a dedicated local service account (pending real-device SQL/SMB proof) and retaining C# 13 during this migration unless a measured Session 01 need justifies an ADR amendment.

### Verification

`git status --porcelain` before edits showed pre-existing untracked `.cc-history/`, `docs/`, and `excute_prompt.md`; these were preserved.

`dotnet build PosAdminTool.sln -c Release` output:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:42.92
```

`dotnet test PosAdminTool.sln -c Release` output:

```text
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 565 ms - PosAdminTool.Domain.Tests.dll (net10.0)

Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, Duration: 1 s - PosAdminTool.Infrastructure.Tests.dll (net10.0)

Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 9 s - PosAdminTool.Application.Tests.dll (net10.0)
```

The runner executed 17 cases (4 + 5 + 8), while the plan's 14 count is the 14 declared test methods; `CURRENT_STATE.md` inventories both accurately.

### Risks and prerequisites for Session 01

- The highest-risk identity decision is documented but not proven: Session 06 must validate SQL login and managed-root ACLs, and Session 12 must validate SMB under the installed service identity.
- Wildcard packages and the C# 13 choice are baseline findings for Session 01; do not change them in this documentation session.
- WinUI was not published: the shared preamble prohibits publish without the assigned session explicitly requesting it and the user authorizing it. Session 00 only requires build and test.

### Next session

Session 01 is unblocked for deterministic toolchain/skeleton work after review of this baseline and the Session 00 commit.

## Session 01 — Deterministic toolchain and solution skeleton

Date: 2026-07-27
Scope: Toolchain, project skeleton, health endpoints, and CI. No business endpoint, no visual design (per task 11 / deliverable boundary).

### Decisions and changes

- `global.json`: pinned SDK `10.0.302` (the installed SDK) with `rollForward: latestPatch` — allows
  later patches in the same feature band, fails closed rather than silently rolling to a newer
  feature band. `allowPrerelease: false`.
- Applied ADR-013 deliberately: `Directory.Build.props` keeps `LangVersion 13.0`, now with a comment
  pointing at the ADR so the choice reads as intentional, not stale.
- Replaced all 7 wildcard `PackageReference` versions with the latest exact version inside the
  *same* major/minor line that was already wildcarded (no unrelated major-version upgrades):
  - `PosAdminTool.Application`: `Microsoft.Extensions.Logging.Abstractions` → `10.0.10`.
  - `PosAdminTool.Infrastructure`: `Microsoft.Data.SqlClient` → `6.1.6`,
    `Microsoft.Extensions.Logging.Abstractions` → `10.0.10`,
    `System.ServiceProcess.ServiceController` → `10.0.10`.
  - `PosAdminTool.WinUI`: `Microsoft.WindowsAppSDK` → `1.8.260710003`, `CommunityToolkit.Mvvm` →
    `8.4.2`, `Microsoft.Extensions.DependencyInjection` → `10.0.10`, `Microsoft.Extensions.Logging`
    → `10.0.10`. Re-grepped the whole tree afterward (`Version="[^"]*\*"` across all `.csproj`,
    range/wildcard scan across all `package.json`); zero matches remain anywhere.
- Enabled `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` repo-wide via
  `Directory.Build.props` and committed a generated `packages.lock.json` for all 10 .NET projects
  (7 pre-existing + 3 new). `dotnet restore --locked-mode` passes.
- Added `src/PosAdminTool.Contracts` (empty class library skeleton; DTOs are Session 02's job),
  `src/PosAdminTool.Agent` (ASP.NET Core 10 Web SDK host), and
  `tests/PosAdminTool.Agent.IntegrationTests`. All three added to `PosAdminTool.sln`.
- Agent (`Program.cs`):
  - Binds loopback-only through a dedicated `LoopbackBinding.ConfigureLoopbackOnly` helper
    (`options.Listen(IPAddress.Loopback, 5001)`), factored out specifically so a test can assert the
    runtime bind instead of trusting it by inspection. `ASPNETCORE_URLS`/`UseUrls` cannot override
    this — explicit `Listen()` calls take precedence in Kestrel.
  - **Anchors `ContentRootPath` to `AppContext.BaseDirectory` explicitly.** Caught during manual
    verification: `WebApplication.CreateBuilder(args)` without this defaults the content root to the
    process's current working directory, not the executable's directory. Launching the published
    exe from a different cwd (as the Windows Service Control Manager will) served 404 for `/` because
    `wwwroot` resolved against the wrong directory. Fixed before it could become a Session 14
    installer surprise.
  - Serves static files + SPA fallback (Production only) when a `wwwroot` is present; otherwise a
    plain 404, so `dotnet run` in Development without an Angular build doesn't need a workaround.
  - `/health/live` and `/health/ready` return `200`.
- Build orchestration (`PosAdminTool.Agent.csproj` MSBuild targets): `dotnet publish` runs
  `npm ci` (only if `node_modules` is missing) then `npm run build` in `src/PosAdminTool.Web`, and
  injects the resulting `dist/web/browser/**` files directly into `ResolvedFileToPublish` under
  `wwwroot/` (bypassing the wwwroot-glob timing problem — Content globs are evaluated before any
  target runs, so files generated mid-build would otherwise be silently dropped from the publish
  output). Verified: the published output's `wwwroot/` contains `index.html` and the built JS/CSS,
  and the published agent needs no Node/npm on the machine that runs it.
- Angular workspace at `src/PosAdminTool.Web`: `ng new web --routing --style=scss --strict
  --standalone --package-manager=npm --ssr=false` (Angular CLI `22.0.8`), plus `ng add
  @angular-eslint/schematics@22.1.0` for `lint`, plus `@playwright/test@1.62.0` for `e2e` (one
  placeholder toolchain spec in `e2e/`; the five real journeys land in their owning sessions per
  plan section 10.3). Explicitly added `"strict": true` and `"strictTemplates": true` to
  `tsconfig.json` — `ng new --strict` did not populate the blanket TS `strict` flag in this CLI
  version, only the granular flags, so re-verifying rather than trusting the scaffold caught a real
  gap against task 6's "strict TypeScript, strict Angular template checks" requirement.
  - All dependency versions pinned exact (no `^`/`~`) in `package.json`; `.nvmrc` and
    `engines.node`/`engines.npm` pinned to the exact installed toolchain (Node `24.18.0`, npm
    `12.0.1`).
  - Dev proxy: `proxy.conf.json` forwards `/api` and `/health` to `http://127.0.0.1:5001`, wired into
    `angular.json`'s `serve.options.proxyConfig`.
- **Angular/Node/TypeScript/RxJS compatibility, read from `https://angular.dev/reference/versions`
  as required (do not trust any version claim elsewhere in the plan)** for Angular `22.0.x`:
  - Node.js: `^22.22.3 || ^24.15.0 || ^26.0.0`
  - TypeScript: `>=6.0.0 <6.1.0`
  - RxJS: `^6.5.3 || ^7.4.0`
  This **confirms** the plan's previously-flagged-unverified TypeScript claim
  (`>=6.0.0 <6.1.0`, plan section 7.1) was correct after all. Installed Node `24.18.0` satisfies
  `^24.15.0`; pinned `typescript@6.0.3` (latest stable 6.0.x) and `rxjs@7.8.2` (latest stable 7.x).
- CI: `.github/workflows/ci.yml` with four separate jobs — `dotnet` (restore --locked-mode/build/test
  on `windows-latest`), `angular` (lint/test/build on `ubuntu-latest`, Node version read from
  `.nvmrc`), `agent-integration-tests` (dedicated re-run of just that test project, its own gate as
  the task requested), and `winui-publish-check` (`dotnet publish` the WinUI project — publish, not
  build, per the shared preamble and `run_app.cmd`).
- Added a standing regression test, `NoWildcardDependencyVersionTests` (in
  `PosAdminTool.Agent.IntegrationTests`), that scans every `.csproj` for `Version="...*"` and every
  `package.json` for a ranged/wildcarded dependency, repo-wide. Also added
  `LoopbackBindingTests.ConfigureLoopbackOnly_NeverBindsToANonLoopbackAddress`, which boots a real
  Kestrel instance (OS-assigned ephemeral port, never the fixed production port, so it can't
  conflict) and asserts every bound address is loopback — automating the plan section 6.2 "a
  security test must assert no non-loopback listener exists" requirement one session earlier than
  strictly required, since the Agent that makes it testable now exists.

### Verification (exact output)

`dotnet --version` → `10.0.302`

`dotnet restore PosAdminTool.sln --locked-mode` → all 10 projects restored, no errors.

`dotnet build PosAdminTool.sln -c Release`:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:05.22
```

`dotnet test PosAdminTool.sln -c Release`:

```text
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 52 ms - PosAdminTool.Domain.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, Duration: 85 ms - PosAdminTool.Infrastructure.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: 608 ms - PosAdminTool.Agent.IntegrationTests.dll (net10.0)
Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 9 s - PosAdminTool.Application.Tests.dll (net10.0)
```

(After adding `LoopbackBindingTests`, `PosAdminTool.Agent.IntegrationTests` grew from 6 to 7 passing
cases; re-run confirmed 4+5+7+8 = 24 total, 0 failed.)

`npm --prefix src/PosAdminTool.Web ci` → `added 573 packages`, 0 errors. (`npm audit`: 6 moderate,
all inside `@angular/cli`'s own optional MCP dev-tooling dependency chain —
`@modelcontextprotocol/sdk` → `@hono/node-server` path-traversal advisory. Dev-time only, not in the
shipped Angular bundle or the Agent runtime. `npm audit fix --force` would downgrade `@angular/cli`
to `21.0.4`, which is incompatible with the plan's required Angular 22 — not applied. Documented
here as an accepted, revisit-if-a-fix-lands item, not fixed.)

`npm --prefix src/PosAdminTool.Web run lint` → `All files pass linting.`

**`npm --prefix src/PosAdminTool.Web run test -- --run` — the plan's literal verification command
fails**: `Error: Unknown argument: run`. Angular 22's `@angular/build:unit-test` (vitest-backed)
builder does not forward arbitrary vitest CLI flags; it exposes its own `--watch` flag and defaults
to a single run automatically outside a TTY. Reporting this exactly rather than silently
substituting a passing command, per the working method's "never describe a command as passing
unless you ran it and saw it pass." The functionally equivalent command,
`npm --prefix src/PosAdminTool.Web run test -- --watch=false` (also verified with plain
`npm run test`, same result), passes:

```text
Test Files  1 passed (1)
     Tests  2 passed (2)
```

Future sessions should use `--watch=false`, not `--run`, against this toolchain.

`npm --prefix src/PosAdminTool.Web run build` → succeeds, output at `dist/web/browser/`
(`main-*.js` 214.71 kB raw / 58.90 kB transfer, `styles-*.css` 0 bytes).

`dotnet publish src/PosAdminTool.Agent/PosAdminTool.Agent.csproj -c Release -r win-x64
--self-contained` → succeeds; Angular build ran automatically as part of publish; published
`wwwroot/` contains `index.html`, `favicon.ico`, and the built JS/CSS.

Started the published agent from a working directory other than the publish folder (simulating how
the Service Control Manager launches a service) and ran the curl/netstat checks:

```text
GET /health/live   -> 200 {"status":"live"}
GET /health/ready  -> 200 {"status":"ready"}
GET /              -> 200, Angular index.html
GET /services      -> 200, SPA fallback to the same index.html
netstat: TCP 127.0.0.1:5001 ... LISTENING   (no 0.0.0.0 or :: entry for :5001)
```

Parity baseline — published, not just built, per `run_app.cmd`:

```text
dotnet publish src/PosAdminTool.WinUI/PosAdminTool.WinUI.csproj -c Debug -r win-x64 --self-contained false
  -> succeeds
POS_ADMIN_SKIP_ELEVATION=true, launched the published exe
  -> confirmed: Get-Process reported MainWindowTitle "WinUI Desktop", a non-zero MainWindowHandle,
     and Responding = True. The window opened.
```

### Standing regression gate

- Secret-scan tests: not yet introduced (Session 03). Not applicable this session.
- Path-policy tests: not yet introduced (Session 02/09/11). Not applicable this session.
- Loopback test: `LoopbackBindingTests` (automated) + manual `netstat` check above. Pass.
- Full existing .NET and Angular unit suites: 24/24 .NET tests pass; Angular default unit test
  (2 tests) passes.

### Risks and prerequisites for Session 02

- `npm audit`'s 6 moderate advisories are transitively inside `@angular/cli` 22.0.8's own dev
  tooling (see above); no upstream fix is available without dropping below Angular 22. Re-check on
  each `@angular/cli` patch bump.
- The plan's and session-prompts' literal `npm run test -- --run` verification command does not
  work against this toolchain version; use `-- --watch=false`. Worth fixing at the source (amend
  both shared documents) in a documentation-only session rather than silently diverging forever.
- A pre-existing, untracked `src/PosAdminTool.Maui/` directory remains on disk (not in
  `PosAdminTool.sln`, not tracked by git). Left untouched per "preserve user changes, never
  overwrite unrelated work" — out of this session's scope to delete unilaterally, but it should be
  removed deliberately in a session that owns cleanup, since it is dead weight against the ~4,200
  line baseline (plan section 0.2).
- `PosAdminTool.Contracts` is an intentionally empty skeleton; Session 02 populates it.
- No secret-scan or path-policy suites exist yet; Session 02 (file browse) and Session 03 (secrets)
  introduce them, after which they join the standing regression gate.

### Next session

Session 02 (Contracts, API conventions, auth, and host file browse) is unblocked.

## Session 02 — Contracts, API conventions, auth, and host file browse

Date: 2026-07-27
Scope: Stable public contracts and cross-cutting API behavior. The only live business-shaped
endpoints introduced are `GET /api/v1/session`, `GET /api/v1/antiforgery`, and the file-browse pair
(`POST /api/v1/files/browse`, `POST /api/v1/files/handles`) — no real privileged operation exists yet.

### Decisions and changes

- **Contracts** (`PosAdminTool.Contracts/V1/**`): ~40 versioned DTOs across Session/Device/
  Configuration/Services/Operations/Backups/Restore/Maintenance/Downloader/Artifacts/Activity/Files,
  plus shared `EvidenceDto`/`FreshnessState`/`PagedResultDto`/`ErrorCodes`/
  `ProblemDetailsExtensionKeys`. None reuse `AppSettings`, `DbDownloaderSettings`,
  `OperationResult`, or `BranchBackupItem` (task 2) — verified structurally, not just by review, via
  `ContractShapeTests` reflection tests (see below). `ServiceActionKind` deliberately omits the
  legacy `Domain.Enums.ServiceControlAction.Delete` value: no plan section or session describes
  service deletion as in-scope, and adding it later needs a plan change plus the full destructive-op
  control set (plan section 6.3), not a silent carry-over.
- **Agent TFM changed to `net10.0-windows10.0.19041.0`** (from Session 01's plain `net10.0`), same
  as Infrastructure/WinUI. Needed for a correct `WindowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator)`
  check — Windows-only per ADR-001, so this only makes an existing constraint explicit, not a new one.
- **Auth**: Negotiate (Windows Integrated), single "LocalAdministratorsOnly" policy. The actual
  group-membership check is behind an injectable `IAdministratorGroupChecker`
  (`WindowsAdministratorGroupChecker` in production, casts to `WindowsPrincipal` and checks
  `WindowsBuiltInRole.Administrator` against the real token — a bare `ClaimsPrincipal.IsInRole` does
  not reliably match Windows group SIDs) so the policy is unit-testable without a real domain.
  `GET /api/v1/session` requires only authentication, not the admin policy, so a non-admin user gets
  a normal `200` with `isAuthorized: false` instead of a bare `403` — the shell needs that to explain
  itself (plan section 7.3).
  - **Real NegotiateHandler is incompatible with the in-memory `WebApplicationFactory` TestServer**:
    it implements `IAuthenticationRequestHandler`, so ASP.NET Core's authentication middleware
    invokes it on *every* request regardless of default scheme, and it throws
    `NotSupportedException` ("requires a server that supports IConnectionItemsFeature like Kestrel")
    immediately. Confirmed by making the failure visible (a throwaway diagnostic test) rather than
    guessing. Fixed by gating `.AddNegotiate()` behind a `Testing:DisableNegotiate` configuration
    flag the test factory sets via `UseSetting`; tests substitute `FakeAuthenticationHandler`
    instead. Verified the real path separately: published and ran the Agent for real, confirmed an
    unauthenticated `GET /api/v1/session` returns a genuine `401` with `WWW-Authenticate: Negotiate`
    from real Kestrel (not the fake). A full interactive authenticated-as-admin round trip against a
    real domain account was not exercised — out of reach in this environment; the authorization
    *policy* itself (admin vs. non-admin vs. unauthenticated) is fully covered by automated tests via
    the fake scheme + injectable group checker.
- **Antiforgery**: double-submit cookie (`XSRF-TOKEN`, deliberately not `HttpOnly` so the SPA can
  read and mirror it — it carries no secret or session identity, only a random anti-CSRF value),
  header `X-CSRF-TOKEN`, bootstrapped via `GET /api/v1/antiforgery`. Applied via a reusable
  `AntiforgeryEndpointFilter` to `POST /api/v1/files/handles` (a mutation — it creates server-side
  handle state) but not `POST /api/v1/files/browse` (read-only despite the POST verb; POST is used
  because the request needs a body, matching the plan's own `POST /api/v1/files/browse` naming).
- **CSP + X-Frame-Options**: `default-src 'self'` etc., `frame-ancestors 'none'`, `X-Frame-Options: DENY`,
  set on every response via early middleware. No permissive CORS was added — same-origin only,
  matching Session 01.
- **Problem Details / correlation IDs / JSON conventions**: `AddProblemDetails()` +
  `UseExceptionHandler()` (never a developer exception page outside Development); a correlation-ID
  middleware generates or echoes `X-Correlation-Id` and injects it into every Problem Details
  response's `extensions.correlationId`; enums serialize as camelCase strings (not raw member names
  or numbers) via a global `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`; a global 1 MB
  Kestrel request-body-size default for JSON API bodies (future upload endpoints override per-route).
- **File browse** (`PosAdminTool.Agent/Files/**`, plan section 5.7 — "the session's most important
  new design"): browse roots come only from `FileBrowseOptions` configuration (empty by default —
  zero roots is the safe starting state until a later session configures real ones), never from the
  request. `FileBrowseService.Resolve` layers defenses in this order: reject any `%` (unresolved
  environment variable) outright rather than expand it; reject `Path.IsPathRooted` or any `:`
  (absolute paths, UNC paths, drive-relative paths, and NTFS alternate-data-stream syntax all
  rejected uniformly); reject any literal `..` path segment; canonicalize via `Path.GetFullPath`
  and *then* re-check containment against the canonicalized root (not a naive string prefix check —
  it appends the directory separator before comparing, closing the sibling-directory-with-a-shared-name-prefix
  bug class); finally walk the resolved path's ancestor chain up to the root checking for
  `FileAttributes.ReparsePoint` at every level, rejecting symlinks/junctions rather than following
  them. A reparse point found while *listing* a directory is excluded from the listing (the rest of
  a legitimate directory still renders); a reparse point found while resolving a *handle* target
  rejects the whole request. Verified against a **real directory junction** (`mklink /J`, not a
  mock) pointing outside the configured root — confirmed rejected.
- **File handles**: `InMemoryFileHandleStore`, single-purpose, single-use (atomic
  `Interlocked.Exchange`-based claim, not a plain bool, to close a TOCTOU race), bound to the issuing
  principal, 5-minute TTL. Order of checks in `Redeem` matters: expiry → principal → purpose → mark
  used — a wrong-principal or wrong-purpose attempt does **not** consume the handle, so the
  legitimate holder can still redeem it once afterward. Clock is injected via `TimeProvider` (not
  `DateTimeOffset.UtcNow` directly) specifically so expiry is unit-testable without a real 5-minute
  sleep.
- **OpenAPI + typed Angular client** (task 7): `Microsoft.AspNetCore.OpenApi` +
  `Microsoft.Extensions.ApiDescription.Server` generate `openapi/PosAdminTool.Agent.json` on every
  `dotnet build` of the Agent (not committed — regenerated). `ng-openapi-gen@1.0.5` turns that into
  an Angular-`HttpClient`-based typed client at `src/app/core/api/generated/` (not committed either).
  All endpoints given explicit `.WithName(...)` + `.Produces<T>()` — without this, minimal API
  response types aren't inferred into the OpenAPI schema and the generated client methods return
  `Observable<StrictHttpResponse<void>>` instead of the real DTO type (caught this by inspecting the
  first generation's output, not by assuming `.Produces` was unnecessary). `npm run build` now runs
  `generate-api-client` (which itself runs `dotnet build` on the Agent) before `ng build`, so the
  literal Session 02 verification command ("generated client compiles under strict TS") is always
  true for that command, not just true if you remember a separate manual step first.
  - **CI consequence**: this moved the `angular` job in `.github/workflows/ci.yml` from
    `ubuntu-latest` to `windows-latest` (matching the `dotnet` job) with a `setup-dotnet` step added,
    because `npm run build` now needs to build a `net10.0-windows`-TFM project. Angular itself is
    OS-agnostic; the toolchain around it, as now wired, is not.
- Fixed `Microsoft.OpenApi` (transitive via `Microsoft.AspNetCore.OpenApi` 10.0.10, resolved to
  2.0.0) to `2.7.5`: NuGet restore flagged a high-severity advisory (GHSA-v5pm-xwqc-g5wc,
  CVE-2026-49451, stack overflow via circular `$ref` in the document *reader*). Our usage only
  *writes* our own document, never parses an untrusted one, so the practical exposure was low, but
  the fix is a free same-major-line version bump, so it was applied rather than accepted as risk.
- Updated `docs/migration/FEATURE_PARITY_MATRIX.md`: corrected the "Browse backup destination" and
  "Select restore source" rows from a stale `/api/v1/browse-sessions` naming (never matched the plan
  or the session prompt) to the actual `/api/v1/files/browse` / `/api/v1/files/handles` endpoints,
  and added rows for the two new cross-cutting endpoints (`/api/v1/session`, `/api/v1/antiforgery`).

### Verification (exact output)

`dotnet build PosAdminTool.sln -c Release`:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

`dotnet test PosAdminTool.sln -c Release`:

```text
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4 - PosAdminTool.Domain.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5 - PosAdminTool.Infrastructure.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:    37, Skipped:     0, Total:    37 - PosAdminTool.Agent.IntegrationTests.dll (net10.0)
Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8 - PosAdminTool.Application.Tests.dll (net10.0)
```

(54 total, 0 failed. `PosAdminTool.Agent.IntegrationTests` grew from Session 01's 7 to 37: 6
health/SPA + loopback/wildcard-scan carried over, plus this session's session-endpoint (3),
file-browse abuse-case (13), handle-lifecycle (6), contract-serialization (3), contract-shape (2),
and Problem-Details-convention (2) tests.)

`npm --prefix src/PosAdminTool.Web run build` (regenerates the OpenAPI doc + typed client, then
builds under strict TypeScript):

```text
Build succeeded. [generate-openapi-document]
Generation from openapi/PosAdminTool.Agent.json finished with 9 models and 1 services. [ng-openapi-gen]
Application bundle generation complete. [1.6s]
Output location: .../src/PosAdminTool.Web/dist/web
```

`npm --prefix src/PosAdminTool.Web run lint` → `All files pass linting.` (generated
`src/app/core/api/generated/**` excluded from lint scope — it is regenerated code, never hand-edited.)

Manual real-agent verification (published `-r win-x64`, launched, not via TestServer):

```text
GET /health/live                    -> 200, no auth required
GET /api/v1/session (no credential) -> 401, WWW-Authenticate: Negotiate (real Negotiate handler, real Kestrel)
GET /                               -> 200 (Angular shell, unaffected by API auth)
netstat: TCP 127.0.0.1:5001 ... LISTENING   (no 0.0.0.0 or :: entry)
```

### Standing regression gate

- Secret-scan tests: not yet introduced (Session 03). Not applicable this session.
- **Path-policy tests: introduced this session** (file-browse abuse cases in `FileEndpointTests`) —
  join the standing gate from here on. Pass.
- Loopback test: `LoopbackBindingTests` (automated, carried from Session 01) + manual `netstat`
  check above against the Session-02-updated Agent. Pass.
- Full existing .NET and Angular unit suites: 54/54 .NET tests pass; Angular default unit test
  (2 tests, unaffected) passes.

### Risks and prerequisites for Session 03

- A full interactive Negotiate round trip (real domain/local admin account, real browser or SSPI
  client) was not exercised in this environment — flagged above. Worth a manual pass on a real
  Windows device before the Session 07 go/no-go gate, alongside the SQL/SMB identity checks that
  gate already requires.
- `PosAdminTool.Contracts` now has real shape; Session 03 should build directly on
  `RedactedConfigurationDto`/`ConfigurationUpdateRequestDto`/`ClearSecretRequestDto` rather than
  reinventing the secret keep/replace/clear contract.
- File-browse roots are configured empty by default; whichever session first needs a real managed
  root (backup destination in Session 08, restore source in Session 09/10) must add it to
  `FileBrowseOptions` deliberately, not assume one exists.
- **Discovered mid-session and left untouched, not part of this session's work**: an `AGENTS.md`
  file and eight empty `.ai/*.md` files appeared in the repository root partway through this
  session, none git-tracked, none created by this session's work. They describe an unrelated
  "AI agent operating instructions" convention this migration's workflow does not use (this
  repository's actual authority is `docs/NET10_ANGULAR22_MIGRATION_PLAN.md` and
  `docs/NET10_ANGULAR22_SESSION_PROMPTS.md`). Not staged or committed here. Flagged for the user —
  possibly a different tool pointed at the same working directory concurrently.
- A pre-existing, untracked `src/PosAdminTool.Maui/` directory still remains on disk (noted in
  Session 01's log too); still out of this session's scope to remove unilaterally.

### Next session

Session 03 (Secure configuration) is unblocked.
