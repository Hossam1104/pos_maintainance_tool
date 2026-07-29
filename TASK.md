# Current Task

- **Task ID:** MIGRATION-SESSION-07
- **Status:** Ready
- **Role:** Implement

## Objective

Deliver Agent-backed Windows-service parity, then stop for the mandatory GO / NO-GO decision.

## Execute

1. Adapt the retained Windows service manager behind authorized Agent commands. Accept only
   server-issued IDs from the configured service list; never accept a service name from the browser.
2. Implement bulk reads, timestamps, transitions, timeouts, cancellation, per-service locks,
   idempotency, audit, and SSE-backed refresh recovery.
3. Build the Services UI with accessible service-context action names and distinct command-sent,
   running, confirmed, and failed states.
4. Produce `docs/migration/GATE_07.md` with observed SQL/service-identity evidence, UI parity,
   scope credibility, and risk status. Stop for an explicit user GO / NO-GO decision.

## Read First

- `src/PosAdminTool.Infrastructure/Windows/WindowsServiceManager.cs`
- `src/PosAdminTool.Agent/Endpoints/OperationEndpoints.cs`
- `src/PosAdminTool.Agent/Operations/OperationRegistry.cs`
- `src/PosAdminTool.Web/src/app/app.routes.ts`
- `docs/migration/UI_PARITY_MAP.md`

## Validation

- `dotnet build PosAdminTool.sln -c Release`
- `dotnet test PosAdminTool.sln -c Release`
- `npm --prefix src/PosAdminTool.Web run test -- --run`
- `npm --prefix src/PosAdminTool.Web run e2e -- --grep "service"`

## Constraints

- Never control an actual RMS or system service without explicit environment authorization.
- Do not start Session 08 without explicit user GO / NO-GO approval.
