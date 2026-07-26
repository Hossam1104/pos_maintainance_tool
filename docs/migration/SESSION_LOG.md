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
