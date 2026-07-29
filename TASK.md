# Current Task

- **Task ID:** MIGRATION-SESSION-06
- **Status:** In Progress
- **Role:** Implement

## Objective

Finish Overview, Device, and Settings parity backed by real Agent data.

The `LocalSystem` SQL identity gate and the initial Session 06 build/test gates have passed.

## Execute

1. Complete the real-Agent configuration E2E flow: import legacy configuration, edit, fake DB test,
   branch verification, save/reload, and prove no secret is returned.
2. Complete Settings behavior for browse roots and SQL-secret keep/replace/clear, including typed
   validation, dirty-form protection, and version conflicts that preserve unsaved values.
3. Bind Overview recent activity and close remaining healthy/degraded/unreachable signal-path
   coverage.

## Read First

- `.ai/HANDOFF.md`
- `src/PosAdminTool.Agent/Endpoints/ConfigurationEndpoints.cs`
- `src/PosAdminTool.Web/src/app/features/settings-page.component.ts`
- `src/PosAdminTool.Web/src/app/features/overview-page.component.ts`
- `src/PosAdminTool.Web/e2e/configuration.spec.ts`
- `tests/PosAdminTool.Agent.IntegrationTests/ConfigurationEndpointTests.cs`

## Validation

- `dotnet build PosAdminTool.sln -c Release`
- `dotnet test PosAdminTool.sln -c Release`
- `npm --prefix src/PosAdminTool.Web run test -- --run`
- `npm --prefix src/PosAdminTool.Web run build`
- `npm --prefix src/PosAdminTool.Web run e2e -- --grep "configuration"`

## Constraints

- Never return, persist, or retain submitted plaintext secrets in Angular.
- Label TCP reachability separately from application-level health.
- Do not edit generated Angular API files or start Session 07.
