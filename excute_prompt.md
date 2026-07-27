# Task Execution

Before starting:

1. Read `AGENTS.md` completely.
2. Read `.ai/CURRENT_STATE.md`.
3. Read only the additional project files required by `AGENTS.md` and this task.
4. Inspect the current Git status and task-related diff.
5. Execute the task below through completion.
6. Run targeted validation.
7. Review the final task-related Git diff.
8. Update `.ai/CURRENT_STATE.md`.
9. Update `.ai/CONTEXT.md` or `.ai/DECISIONS.md` only when materially required.

Do not stop after summarizing or planning unless the task explicitly requests planning only.


# TASK AND Objective

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
