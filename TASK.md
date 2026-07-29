# Current Task

- **Task ID:** MIGRATION-SESSION-05-GATE
- **Status:** Blocked
- **Owner:** Unassigned
- **Role:** Debug

## Objective

Close the remaining Session 05 standing regression gate by diagnosing why
`OperationEndpointTests.DestructiveDiagnostic_WritesExactlyOneSanitizedAuditRecord` passes in
isolation but fails during the full solution test run, then rerun the full gate.

## Done When

- The audit integration test is deterministic in isolated and full-suite execution.
- The underlying production or fixture race/lifecycle defect is fixed without weakening the audit assertion.
- `dotnet test PosAdminTool.sln -c Release --nologo` passes all 98 tests.
- Session 05 verification and shared memory record the passing result.
- `.ai/HANDOFF.md` is set to `Empty`; Session 06 is not activated before this gate closes.

## Scope

### Read First

- `.ai/HANDOFF.md`
- `docs/migration/SESSION_LOG.md`, Session 05 only
- `tests/PosAdminTool.Agent.IntegrationTests/OperationEndpointTests.cs`
- `tests/PosAdminTool.Agent.IntegrationTests/AgentWebApplicationFactory.cs`
- `src/PosAdminTool.Agent/Audit/OperationAuditWriter.cs`
- `src/PosAdminTool.Agent/Operations/OperationWorker.cs`

### May Change

- The Agent audit/operation code and integration-test fixture directly responsible for the failure
- `docs/migration/SESSION_LOG.md`
- Task-related `.ai/` memory files

### Out of Scope

- Session 06 endpoints, UI, or configuration flows
- Changes to the Session 05 Angular shell
- Weaker timing, sanitization, or audit-count assertions
- WinUI removal, installer work, deployment, commit, push, or pull-request creation

## Current Evidence

- Session 05 implementation is committed at `ef7803a`.
- The targeted test passed 1/1 on 2026-07-29.
- The immediately following full solution run failed 1/98 because
  `audit/operations.jsonl` did not exist when the test read it.
- This isolation/full-suite difference indicates a deterministic ordering, lifecycle, or completion-observation issue that remains unresolved.

## Next Action

Reproduce the full-suite interaction with focused diagnostics, identify whether operation state is
published before audit persistence completes or whether shared fixture cleanup races the audit
write, implement the smallest supported fix, and rerun targeted then full validation.
