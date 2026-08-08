# Current Task

- **Task ID:** POS-M02
- **Status:** Ready for owner authorization; POS-M01 complete
- **Role:** Implement
- **Source:** `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`

## Authorized Session Prompt

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
