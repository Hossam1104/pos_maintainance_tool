# Current Task

- **Task ID:** MIGRATION-SESSION-08
- **Status:** In Progress
- **Role:** Implement

## Objective

Move the local backup workflow into the Agent operation engine with safe archive artifacts and a
browser select/review/run/progress/result/catalog experience.

## Execute

1. Put BackupService file-system and shell behavior behind ports while retaining the selectable
   branch/cashier databases and three appsettings files.
2. Require an allowlisted destination browse handle; validate destination, free space, components,
   branch/database identity, and configuration sources before work starts.
3. Create human-readable archives with a versioned manifest, checksums, artifact metadata, safe
   streaming download, persisted operation progress, cancellation, cleanup, and audit.
4. Build the select/review/run/progress/result/catalog UI; retain branch and target database at review
   and replace Explorer launch with resolved destination copy plus artifact download.

## Read First

- `src/PosAdminTool.Application/Services/BackupService.cs`
- `src/PosAdminTool.Agent/Operations/OperationRegistry.cs`
- `src/PosAdminTool.Agent/Endpoints/FileEndpoints.cs`
- `src/PosAdminTool.Web/src/app/core/agent-api.service.ts`
- `docs/migration/UI_PARITY_MAP.md`

## Validation

- `dotnet build PosAdminTool.sln -c Release`
- `dotnet test PosAdminTool.sln -c Release`
- `npm --prefix src/PosAdminTool.Web run test -- --run`
- `npm --prefix src/PosAdminTool.Web run e2e -- --grep "backup"`

## Constraints

- Never execute `BACKUP DATABASE` against a real database without explicit authorization.
