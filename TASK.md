# Current Task

- **Task ID:** MIGRATION-SESSION-09
- **Status:** Ready
- **Role:** Implement
- **Source:** `docs/NET10_ANGULAR22_SESSION_PROMPTS.md` (the requested `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md` is absent)

## Authorized Session Prompt

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
  dotnet test PosAdminTool.sln -c Release

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
