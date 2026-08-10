# Current Task

- **Task ID:** CLAUDE OPUS 5 R2 FINAL PRE-INTEGRATION REVIEW
- **Status:** REVIEW REQUIRED / NO EXECUTION AUTHORIZED
- **Role:** Review
- **Source:** `docs/POS_SUPPORT_HUB_MERGE_PREPARATION_PLAN.md` and `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`
- **Authorization:** Not granted. Review the completed POS-M06 candidate only; do not implement, merge repositories, or integrate Angular.

## Review scope

Review the completed POS-M06 final audit and its task-scoped merge to POS `main`.

Confirm, with evidence:

1. POS-M01 through POS-M06 are recorded complete and the POS status is `MERGE-READY CANDIDATE`.
2. The source, architecture, contracts, security boundaries, repository hygiene, landing map,
   retained WinUI, and validation evidence recorded in the canonical plan are consistent with the
   current POS tree.
3. The following remain open and are not misrepresented as passed: ADR-012 LocalSystem/Session 0
   SMB, live Agent loopback/Negotiate/local-admin/antiforgery/SSE evidence, real privileged
   SQL/SCM/restore/maintenance/downloader evidence, remote trigger reconciliation/idempotency,
   and Support Hub final deployment/proxy topology.
4. `STANDALONE ANGULAR EXPANSION: FROZEN`, `WINUI: RETAINED UNTIL CROSS-PROJECT DECISION`,
   `REPOSITORY MERGE: NOT AUTHORIZED`, and `RMS+ SUPPORT HUB INTEGRATION: NOT AUTHORIZED` remain
   explicit.
5. The next action is owner-approved cross-project integration planning only after this review;
   no integration implementation task may be copied into `TASK.md`.

## Review boundary

This is a review-only gate. Do not modify the RMS+ Support Hub repository. Do not add POS
features, standalone Angular screens, deployment/proxy decisions, WinUI removal, repository
history rewrites, or real destructive/device operations. If a material contradiction is found,
record it in `.ai/HANDOFF.md` and keep POS out of integration planning until resolved.

## Stop

Return the R2 review outcome and stop. Do not execute any cross-project integration session.
