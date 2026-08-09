# Current Task

- **Task ID:** POS-M05
- **Status:** PENDING OWNER AUTHORIZATION
- **Role:** Implement
- **Source:** `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`

## Authorized Session Prompt

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
