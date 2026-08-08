# Shared AI Operating Contract

This file is the canonical instruction source for Codex, Claude, and Kimi.
Claude loads it through `CLAUDE.md`. Do not duplicate these instructions elsewhere.

## Shared-brain principle

The shared brain is the repository, Git, `TASK.md`, and the concise files under `.ai/`.
`.ai/HISTORY.md` indexes completed milestones; it is not an active task queue.
Chat transcripts and hidden model reasoning are not project records.
Never ask another model to reconstruct the project from previous conversations.

## Mandatory startup

For any task:

1. Read `TASK.md`.
2. Read `.ai/STATE.md`.
3. Run `python .ai/scripts/context.py`.
4. Read `.ai/HANDOFF.md` only when its status is `In Progress` or `Blocked`.
5. Read only the source, tests, and documentation named in `TASK.md`, plus task-related changed files.
6. Read `.ai/PROJECT.md` only when non-obvious stable context is required.
7. Read `.ai/DECISIONS.md` only when the task may affect an existing decision; open a detailed ADR only when its affected area matches the task.
8. Read `.ai/HISTORY.md` only when reconciling milestone status or auditing completed work.

## Authorized session workflow

The presence of a ready `TASK.md` is not owner authorization. For every owner-authorized Luna session, follow this exact repository workflow:

1. Read `TASK.md`; 2. read `.ai/STATE.md`; 3. load only task-relevant context; 4. confirm review
   and entry gates.
5. Require a clean, synchronized `main`; 6. create the task branch; 7. execute exactly one
   task/session; 8. run targeted validation.
9. Review the task-scoped diff; 10. update the canonical plan; 11. update `.ai/STATE.md`;
   12. update `.ai/HANDOFF.md` only when needed.
13. Copy the complete next session from `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`
    into `TASK.md`; 14. commit task-scoped work; 15. push the session branch.
16. Merge safely to `main`; 17. push `main`; 18. verify `origin/main...main == 0 0`; 19. delete
    the completed local and remote session branch when safe; 20. stop without executing the next task automatically.

For this repository, the active programme files are `docs/POS_SUPPORT_HUB_MERGE_PREPARATION_PLAN.md` and `docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`; old migration files are historical evidence only after Session 08 and must not authorize Sessions 09-14.

Do not automatically read `.ai/archive/`, every requirement or design document, the full
repository, full Git history, full unrelated diffs, old model transcripts/exported sessions, or
completed milestone history during ordinary implementation startup.

## One active owner

Only one model owns implementation at a time.
The next model continues from the repository state instead of repeating discovery, planning, or completed work.

Treat these sources in this order:

1. Current code and tests
2. Current Git status and task-related diff
3. `TASK.md`
4. `.ai/HANDOFF.md` for incomplete work
5. `.ai/STATE.md`
6. Stable project documentation and decisions

Challenge previous work only when there is concrete evidence of a defect, contradiction, security risk, or unmet acceptance criterion.

## Roles

The role is declared in `TASK.md`:

- `Plan`: inspect only enough context to produce an executable plan; do not implement.
- `Implement`: execute the accepted plan or objective; do not restart planning without a blocker.
- `Debug`: reproduce, isolate, fix, and validate.
- `Review`: inspect the task-related diff and tests only; do not modify unless requested.
- `Test`: validate the changed scope and report evidence.

Small, well-scoped tasks should use one model only. Use multiple models only when the task benefits from a separate planning, implementation, or review checkpoint.

## Execution rules

- Work only within the requested scope.
- Prefer existing patterns and utilities.
- Avoid unrelated refactoring, formatting, and dependency changes.
- Preserve `PosAdminTool.WinUI` until the explicit Session 14 parity approval and cutover.
- Keep C# 13, exact dependency versions, and committed lockfiles unless the active task explicitly changes the toolchain decision.
- Keep the Agent Windows x64, per-device, same-origin, and loopback-only; never add LAN/public binding or expose secrets or absolute host paths through browser contracts.
- Do not add SQLite, SignalR, a PWA, a service worker, or IndexedDB without an approved decision change.
- Do not edit generated Angular API files under `src/PosAdminTool.Web/openapi/` or `src/PosAdminTool.Web/src/app/core/api/generated/`.
- Use Windows for Agent/OpenAPI generation. Validate the retained WinUI runtime with `dotnet publish`, because plain build does not stage all unpackaged WinUI resources.
- Do not paste large files into project-memory documents.
- Do not store raw logs, complete diffs, test output, credentials, URLs with secrets, connection strings, or personal data in `.ai/`.
- Do not commit, push, deploy, or run destructive commands unless explicitly requested. The
  authorized session workflow above is the explicit repository workflow when the owner has
  authorized that session.
- For minor uncertainty, make the safest reversible assumption and record it in the handoff only if another model needs it.
- Ask the user only for a material business decision, unavailable access, or unsafe/destructive action.

## Validation

- Run targeted checks for the changed scope first.
- Run a broad build or regression suite only when the impact is broad or `TASK.md` requires it.
- Distinguish new failures from pre-existing failures.
- Never claim a check passed unless it ran successfully.
- Before completion, inspect the final task-related diff and remove temporary/debugging changes.

## Memory update policy

After completed work:

- Update `.ai/STATE.md` only with durable current facts; replace outdated text rather than appending history.
- Set `.ai/HANDOFF.md` to `Empty`.
- Add one concise evidence-linked milestone entry to `.ai/HISTORY.md` when a session or project milestone is fully complete.
- Replace `TASK.md` with the next active or blocked item; do not leave a completed task marked ready.
- Remove standalone execution-prompt copies after their task is complete. Keep future prompts in the canonical runbook.
- Update `.ai/PROJECT.md` only when stable architecture, commands, integrations, or non-obvious conventions changed.
- Update `.ai/DECISIONS.md` only for a lasting decision. Put detailed rationale in one ADR under `.ai/decisions/`.
- Move a large completed plan to `.ai/archive/` only when it has audit value; otherwise delete it.

When stopping before completion:

- Update `.ai/HANDOFF.md` with only the delta: completed work, exact next action, changed files, validation, blocker, and risks.
- Keep the handoff below 40 lines.
- Do not rewrite the full project state or implementation history.
- Keep incomplete work out of `.ai/HISTORY.md`; record it in `TASK.md` and `.ai/HANDOFF.md`.

## Completion response

Return only:

### Result
Completed, Partially Completed, Blocked, Planning Completed, or Review Completed.

### Changes
Concise task-related changes.

### Validation
Commands executed and results.

### Git
Branch, commit, merge, push, synchronization, and branch-deletion results when authorized.

### Remaining
Only unresolved work, blockers, or risks.
