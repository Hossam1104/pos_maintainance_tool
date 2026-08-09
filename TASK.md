# Current Task

- **Task ID:** POS-M03
- **Status:** Ready for owner authorization
- **Role:** Implement
- **Source:** `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`

## Authorized Session Prompt

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
