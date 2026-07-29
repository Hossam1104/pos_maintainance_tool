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
