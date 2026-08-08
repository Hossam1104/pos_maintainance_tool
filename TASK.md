# Current Task

- **Task ID:** POS-M01
- **Status:** Ready for owner authorization; not executed in the reconciliation session
- **Role:** Implement
- **Source:** `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`

## Authorized Session Prompt

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
