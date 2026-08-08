# POS -> RMS+ Support Hub Preparation Session Prompts

> **ACTIVE canonical execution prompt source.** These prompts replace the standalone execution
> direction of `docs/NET10_ANGULAR22_SESSION_PROMPTS.md` after completed Session 08.

> A prompt is not authorization. The owner must authorize exactly one session, and the repository
> workflow in `AGENTS.md` must be followed. Do not execute POS-M01 as part of the reconciliation
> that creates this file.

## Shared execution contract

Use this contract together with exactly one POS-M prompt:

```text
You are the primary executor and repository maintainer for Hossam1104/pos_maintainance_tool.

First read TASK.md, .ai/STATE.md, and the task-relevant sections of this prompt file and
POS_SUPPORT_HUB_MERGE_PREPARATION_PLAN.md. Run `python .ai/scripts/context.py`. Inspect current
source, tests, Git status, and the entry/review gates before editing. Do not reconstruct the
project from conversation history.

The accepted baseline is Sessions 00-08. Preserve PosAdminTool.Domain, PosAdminTool.Application,
PosAdminTool.Infrastructure, PosAdminTool.Contracts, PosAdminTool.Agent, the retained
PosAdminTool.WinUI, the existing PosAdminTool.Web implementation, tests, ADRs, security boundaries,
backup implementation, service-control implementation, configuration implementation, and
operation engine.

The POS repository remains separate temporarily. Standalone Angular expansion is frozen. POS owns
Domain/Application behavior, privileged Windows/SQL/SMB operations, configuration/secrets,
contracts, authorization, operation execution, audit, and portability. RMS+ Support Hub owns the
final Angular shell, global navigation, shared components, branding, design system, themes,
cross-tool UX, and final POS route integration.

Never add SQLite, SignalR, a PWA, a service worker, IndexedDB, LAN/public binding, cloud/remote
management, or a role matrix. Keep the Agent Windows x64, per-device, same-origin, and
loopback-only. Never expose secrets, credentials, raw exception text, absolute host paths,
connection strings, or personal data through browser contracts or project memory.

Use only disposable fakes and temporary directories. Never restore a real database, overwrite real
RMS files, call production endpoints, control a real Windows service, or use a production SMB share.
Do not edit generated files under `src/PosAdminTool.Web/openapi/` or
`src/PosAdminTool.Web/src/app/core/api/generated/`.

Implement exactly the assigned POS-M session. Run targeted validation first, review the task-scoped
diff, update the canonical plan and project memory, copy the complete next authorized prompt into
TASK.md only as required by the workflow, and stop at the prompt boundary. Do not execute the next
session automatically.
```

## POS-M01 - Runtime State Boundedness & Session 08 Architecture Corrections

```text
Role:
Implement one owner-authorized backend correction session. Do not add a new frontend feature.

Objective:
Close architecture issues discovered after Session 08 before adding more privileged workflows.
The current OperationRegistry queue is bounded at Capacity = 32, but the queue capacity is not a
retention policy. Make runtime state genuinely bounded while preserving the ADR-approved in-memory
architecture.

Entry conditions:
- Sessions 00-08 are accepted and preserved.
- Read the current OperationRegistry, OperationWorker, ArtifactCatalog, file-handle store, audit
  writer, relevant Agent tests, and the current plan before editing.
- POS-M01 is the only implementation session in scope.

Required work:
1. Verify and correct bounded retention for OperationRegistry `_entries`.
2. Verify and correct idempotency retention for `_idempotency`. Define behavior when an old
   completed operation has been evicted and ensure active duplicate detection remains correct.
3. Evict completed operation records deterministically by an explicit, injectable/testable policy
   (age, count, or a documented combination). Never evict active queued/running operations.
4. Bound each operation's event list without losing the state transition and required error/audit
   evidence. Keep browser messages sanitized, length-bounded, newline-safe, and free of secrets,
   absolute host paths, and raw exception text.
5. Bound activity/list output through the same explicit policy rather than allowing an unbounded
   derived view.
6. Inspect and correct ArtifactCatalog retention. Do not delete an artifact while it is legitimately
   downloadable. Define the metadata/file lifecycle, expiry or retention rule, principal scoping,
   and not-found behavior after safe expiry.
7. Verify cancellation cleanup and resource disposal on queued cancellation, running cancellation,
   success, failure, worker shutdown, lock acquisition cancellation, and backup staging cleanup.
8. Preserve REST-as-state-truth, SSE-as-transport, principal-scoped idempotency, resource locks,
   destructive audit behavior, operation state transitions, and active operation visibility.
9. Reconcile stale Session 08 documentation and validation statements. The current recorded
   Session 08 result is 125 .NET tests, 8 Angular tests in 6 files, the backup E2E gate, and WinUI
   publish; old 97/98 claims must not be presented as current.

Implementation constraints:
- No SQLite, durable operation database, SignalR, browser storage, or unrelated refactor.
- Keep behavior compatible for active callers and existing contracts unless a not-found/retention
  consequence must be documented.
- Prefer a single deterministic retention policy with TimeProvider or another test clock.
- Do not hide memory growth by changing only the queue capacity.
- Add focused tests for retention, idempotency, active-operation preservation, event bounds,
  artifact download/expiry, cancellation cleanup, and sanitized messages.

Required focused tests:
- Completed entries are evicted at the deterministic limit/clock boundary.
- Queued/running entries are never evicted by completed retention.
- Eviction removes or expires the corresponding idempotency record safely.
- Reusing a retained idempotency key returns the existing operation; an evicted operation follows
  the documented safe behavior.
- Event and activity retention remain bounded while final state and required evidence survive.
- An artifact remains downloadable while within its legitimate retention window and is never deleted
  before the operation/catalog contract permits it.
- Expired/missing artifacts fail closed without revealing a host path.
- Cancellation releases resources and removes temporary/staging state on every relevant exit path.
- Operation messages remain sanitized and bounded.

Verification:
  dotnet build PosAdminTool.sln -c Release --no-restore
  dotnet test PosAdminTool.sln -c Release --no-restore
  git diff --check

Review checklist:
- Inspect the task-scoped diff for source changes only in POS-M01 areas.
- Confirm no generated Angular files, real RMS targets, secrets, or absolute host paths changed.
- Update the canonical plan and `.ai/STATE.md` with durable facts only.
- Copy the complete POS-M02 prompt into TASK.md for the next owner-authorized session.

Stop:
Do not implement Restore, Cleanup, DB Downloader, or any Angular feature in this session. Do not
execute POS-M02 automatically.
```

## POS-M02 - Restore Backend & Archive Hardening

```text
Role:
Implement one owner-authorized backend-only restore preparation session. No standalone Angular UI.

Objective:
Adapt the valuable security/backend requirements of historical Session 09 into a merge-ready POS
restore backend. Never restore real RMS data.

Entry conditions:
- POS-M01 is complete and its focused/full tests pass.
- Read the current restore contracts, legacy RestoreService, Agent operation patterns, file-browse
  handles, configuration/secrets, SQL adapter, ADRs, and POS preparation plan.
- Use only disposable fakes, test SQL abstractions, and temporary directories.

Required backend capability:
1. Keep two source mechanisms distinct: a bounded streamed browser upload and selection of an
   existing device file through a principal/purpose-bound browse handle. A multi-gigabyte `.bak`
   already on the device must not be uploaded through the browser.
2. Enforce upload limits and clean staging files on rejection, cancellation, failure, and shutdown.
3. Inspect/validate the archive before extraction: entry paths, absolute paths, parent traversal,
   entry count, expanded byte total, compression ratio, permitted extensions, duplicate names,
   reparse/junction/symlink escape, manifest/checksum, branch identity, and destination mappings.
4. Reject unknown JSON files, ambiguous or multiple `.bak` candidates, wrong branch, checksum
   mismatch, ZIP-bomb ratio, excessive entry count, and excessive expanded size.
5. Build a server truth restore preview containing target database, logical SQL files, MOVE
   destinations, config overwrites, affected services, required free space, warnings, and any
   rejection reason. Never accept a client-guessed host path or mapping.
6. Require a short-lived one-use server challenge and typed confirmation for overwrite execution.
   Recompute all policy at execute time; stale, reused, changed, or expired previews fail closed.
7. Use operation IDs, durable-in-memory stages, cancellation, resource locks, sanitized progress,
   principal-scoped idempotency, audit records, and post-restore verification.
8. Support full, database-only, and config-only modes at the API/backend contract level.
9. Keep SQL logical-file discovery and MOVE planning behind testable interfaces. Never run a real
   restore during tests.

Required tests:
- Valid old-format and new-format archives restore using fakes.
- Reject path traversal, absolute paths, junction/symlink/reparse escape, ZIP-bomb ratio,
  excessive entry count, excessive expanded size, checksum mismatch, duplicate names, multiple
  ambiguous `.bak` files, wrong branch, unknown JSON files, and unsafe destinations.
- Verify SQL logical-file mapping and MOVE planning.
- Verify restore failure, config-copy failure, mid-operation interruption/cancellation, resource
  lock conflict, and post-check failure handling.
- Verify stale, reused, expired, changed, and principal-mismatched preview challenges fail closed.
- Verify upload size limits and no staging-file residue after rejection/cancellation/failure.
- Verify full, database-only, and config-only mode contracts.
- Verify no secret or absolute host path appears in API responses, operation messages, or audit.

Verification:
  dotnet build PosAdminTool.sln -c Release --no-restore
  dotnet test PosAdminTool.sln -c Release --no-restore
  git diff --check

No Angular work:
Do not implement Restore route, dialogs, visual design, screenshots, or standalone UI. Preserve
the behavioral requirements for later Support Hub integration acceptance criteria.

Stop:
Never restore a real database or overwrite real RMS files. Do not implement POS-M03 automatically.
```

## POS-M03 - Cleanup & Branch Reset Backend Safety

```text
Role:
Implement one owner-authorized backend-only maintenance safety session. No standalone Angular UI.

Objective:
Extract the security-critical backend portion of historical Session 11 and replace the unsafe
legacy client-driven cleanup/reset behavior with an enforceable Agent boundary.

Entry conditions:
- POS-M01 is complete; POS-M02 is complete if it affects shared operation/challenge/locking code.
- Read CleanupService, reset/database interfaces, current Agent operation/audit/lock patterns,
  configuration path options, contracts, and the POS preparation plan.

Required backend capability:
1. Define a canonical managed-path policy. Canonicalize first, then check containment against
   configured managed roots and a protected-root denylist.
2. Validate environment-variable expansion, drive-relative paths, UNC policy, junction/reparse/
   symlink escapes, install/data-directory separation, and root-level targets.
3. Build cleanup and branch-reset previews that report exact server-resolved services, paths,
   branch/database/table scope, counts where safely queryable, free-space/recovery warnings, and
   every policy rejection without exposing sensitive paths in browser contracts.
4. Require the authorized local-administrator principal, a fresh one-use challenge, typed branch
   or phrase confirmation, principal-scoped idempotency, resource locks, cancellation, and an
   immutable sanitized audit record.
5. Recompute every policy and target immediately before execution. Never trust preview conclusions.
6. Preserve explicit partial-failure semantics: stop/continue policy, per-item result, cleanup
   residue, and recovery guidance must be represented in operation state.
7. Keep service control and SQL reset behind interfaces and fake them in tests.

Required tests:
- Canonical path containment rejects traversal, absolute un-managed roots, protected roots,
  drive/UNC policy violations, environment-variable escapes, and reparse/junction/symlink escapes.
- Preview is server-derived and a changed target/policy between preview and execute fails closed.
- Fresh, reused, expired, principal-mismatched, and wrong typed confirmations fail closed.
- Concurrent conflicting operations are serialized/rejected by resource locks and idempotency.
- Cancellation, service-stop failure, file-delete failure, SQL reset failure, and partial failure
  produce safe, auditable outcomes without unsafe continuation.
- No raw paths, credentials, or exception details appear in browser contracts, messages, or audit.
- Disposable fake roots and fake service/database managers are used exclusively.

Verification:
  dotnet build PosAdminTool.sln -c Release --no-restore
  dotnet test PosAdminTool.sln -c Release --no-restore
  git diff --check

No Angular work:
Do not create the final standalone Angular Maintenance route or destructive UI. Preserve the
server-derived preview and confirmation behavior as Support Hub integration acceptance criteria.

Stop:
Never delete real files, reset a real database, stop a real service, or execute POS-M04
automatically.
```

## POS-M04 - DB Downloader Backend & SMB Portability

```text
Role:
Implement one owner-authorized backend/portability session. No standalone Angular Downloader UI.

Objective:
Extract the reusable backend portion of historical Session 12, preserve current downloader
behavior, and make the capability safe under the Agent/service-identity boundary.

Entry conditions:
- POS-M01 is complete; POS-M03 is complete if shared operation, artifact, or resource-lock code is
  changed.
- Read DbDownloadService, BackupApiClient, SMB repository/scope/path resolver, downloader settings,
  Agent operation/artifact contracts, secret store, and ADR-012.

Required backend capability:
1. Model downloader work as an Agent operation with operation ID, per-branch progress, state truth,
   idempotency, cancellation, timeout, resource locks, sanitized messages, and audit where required.
2. Preserve backup trigger behavior, newest-created-folder discovery, exact branch ZIP matching,
   stable-size observation, independent per-branch progress, timeouts, and partial outcomes.
3. Enforce SSRF defenses for the trigger endpoint: safe schemes, approved target policy, no loopback/
   metadata/private-network bypass beyond the explicit local policy, bounded requests, and no
   production calls in tests.
4. Validate SMB/UNC target policy, canonical roots, safe filenames, share behavior, cancellation,
   connection cleanup, and credential isolation. Never send RDB credentials to the browser.
5. Use principal-scoped opaque artifact IDs and safe streamed download behavior. Do not return raw
   UNC paths, connection strings, or server credentials.
6. Validate service identity behavior and document the exact evidence gate for LocalSystem/Session 0
   SMB behavior. If a representative-device proof cannot safely be obtained, record the exact gate;
   do not infer success.

Required tests:
- Newest-created-folder selection and exact branch ZIP matching are preserved.
- Stable-size observation, per-branch progress, timeout, cancellation, partial completion, retry,
  and failure semantics are deterministic under fake clocks/adapters.
- Unsafe URL schemes, host forms, redirects, private/metadata targets, malformed branches, unsafe
  SMB roots, and path traversal fail closed.
- Credentials never appear in API responses, logs, audit, operation messages, or artifacts.
- Artifact IDs are opaque and principal-scoped; streamed downloads are cancellable and safe.
- SMB connection scope is disposed on success, failure, cancellation, and timeout.
- Service identity validation is tested with fakes and its representative-device gate is documented.

Verification:
  dotnet build PosAdminTool.sln -c Release --no-restore
  dotnet test PosAdminTool.sln -c Release --no-restore
  git diff --check

No Angular work:
Do not build the final standalone Downloader feature. Preserve backend behavior and acceptance
criteria for Support Hub integration.

Stop:
Do not call real Production endpoints, real SMB shares, or a real service identity. Do not execute
POS-M05 automatically.
```

## POS-M05 - Support Hub Landing Map, Dependency Collision Audit & Repository Portability

```text
Role:
Implement one owner-authorized planning/governance and safe repository-hygiene session. Do not
merge repositories and do not expand standalone Angular.

Objective:
Prepare exact future import/integration boundaries after POS-M01 through POS-M04. The canonical
plan must be sufficient for a separate cross-project review without guessing file ownership.

Entry conditions:
- POS-M01, POS-M02, POS-M03, and POS-M04 are complete, or any exception is explicitly recorded.
- Read the current source tree, project files, package/lock files, test projects, Angular workspace
  metadata, generated-file rules, ADRs, the canonical POS preparation plan, and the available
  Support Hub integration material.

Required plan outputs:
1. Project-level landing map.
2. File-level landing map for Domain, Application, Infrastructure, Contracts, Agent, WinUI, Web,
   tests, scripts, resources, and documentation.
3. Namespace strategy and public-contract compatibility strategy.
4. Dependency ownership and NuGet/npm collision map with exact-version implications.
5. DI ownership, configuration ownership, logging/audit ownership, Agent ownership, Angular
   ownership, test ownership, resource ownership, and scripts/build ownership.
6. `.gitignore`, generated-output, documentation, and security-contract collision analysis.
7. A disposition for every significant source area using one of:
   KEEP AS-IS; KEEP WITH RENAME; MOVE DURING MERGE; ADAPT DURING MERGE; REFERENCE ONLY;
   DO NOT COPY - SUPPORT HUB ALREADY OWNS IT; RETIRE LATER; NEEDS CROSS-PROJECT DECISION.
8. A clear list of residual POS risks, evidence gates, and decisions that cannot be made in the
   POS repository alone.

Safe repository hygiene may include correcting stale references, removing accidental temporary
outputs, or aligning documentation links when the change is clearly task-scoped. Do not do broad
formatting, unrelated refactoring, dependency upgrades, generated-file edits, repository merge,
branch rewrite, or production changes.

Required review outcome:
At completion, update TASK.md with the complete POS-M06 prompt and mark it blocked pending:

  CLAUDE OPUS 5 REVIEW REQUIRED

Do not present a ready TASK.md as owner authorization. Do not begin POS-M06 before the R1 review and
explicit owner authorization.

Verification:
  git diff --check
  python .ai/scripts/check_memory.py
  rg -n "POS-M01|POS-M02|POS-M03|POS-M04|POS-M05|POS-M06|Support Hub|superseded" TASK.md .ai docs/POS_SUPPORT_HUB_MERGE_PREPARATION_PLAN.md docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md

Review checklist:
- No repository merge occurred.
- No standalone Angular feature or global visual system was added.
- WinUI remains present and buildable/publishable.
- Active plan and prompts are the only active POS preparation documents.
- Old Sessions 09-14 are visibly historical/deferred and cannot be copied as authorized prompts.

Stop:
Stop after the landing/collision audit. Request Claude Opus 5 R1 review; do not execute POS-M06.
```

## POS-M06 - Final Merge-Ready Candidate Audit

```text
Role:
Perform one owner-authorized, review-gated final POS preparation audit. Do not merge repositories
or integrate Angular.

Entry conditions (all required):
- POS-M01 through POS-M05 are complete.
- Claude Opus 5 R1 review has completed and its findings are addressed or explicitly accepted.
- The owner explicitly authorizes POS-M06 continuation.
- The workspace is clean and synchronized according to AGENTS.md.

Verify, with evidence:
1. Domain/Application portability and absence of privileged host leakage.
2. Infrastructure isolation for SQL, SCM, SMB, filesystem, configuration, and secrets.
3. Agent loopback binding, Negotiate/local-admin authorization, antiforgery, redaction, correlation,
   safe Problem Details, operation contracts, and audit behavior.
4. Bounded runtime state: operation entries, idempotency, events, activity, artifacts, cancellation,
   and resource cleanup.
5. Restore backend archive safety, preview/challenge/execute-time policy, locks, cancellation, and
   post-restore verification.
6. Cleanup/reset path policy, protected roots, previews, challenge/recomputation, locks, audit,
   and partial failure semantics.
7. DB Downloader backend, SSRF/SMB policy, credential isolation, cancellation, artifacts, and the
   exact service-identity evidence gate.
8. Configuration ownership, secret handling, operation messages, and no browser path/secret leak.
9. Stable versioned contracts and generated-file hygiene.
10. Repository cleanliness, namespace/dependency/DI/config/logging/test/resource ownership, landing
    map, collision analysis, and no duplicate standalone Angular plan.
11. Retained WinUI presence and required publish/buildability evidence.
12. Documentation consistency and Git cleanliness.

Required output if every gate passes:

  POS PREPARATION:
  COMPLETE

  POS STATUS:
  MERGE-READY CANDIDATE

  STANDALONE ANGULAR EXPANSION:
  FROZEN

  WINUI:
  RETAINED UNTIL CROSS-PROJECT DECISION

  REPOSITORY MERGE:
  NOT AUTHORIZED

  NEXT:
  WAIT FOR RMS+ SUPPORT HUB SESSION 08 AND CROSS-PROJECT REVIEW

Required validation:
Run the targeted checks for every changed area, the full agreed .NET/Angular gates where the
environment supports them, `git diff --check`, memory checks, and the retained WinUI publish gate.
Report actual results and distinguish unavailable representative-device evidence from passing fake
tests.

Stop:
If any safety, security, portability, build, documentation, or collision gate fails, do not call
the repository merge-ready. Record the blocker, update TASK.md/HANDOFF.md, and stop. Even a
successful POS-M06 audit does not authorize a repository merge or Angular integration.
```

## Review gates

- **R1 - Claude Opus 5:** required after POS-M05 and before POS-M06.
- **R2 - Claude Opus 5:** required after POS-M06 and before any repository integration.
- A critical finding may trigger an earlier review.
- Owner authorization is required at each gated continuation.
