using PosAdminTool.Agent.Antiforgery;
using PosAdminTool.Agent.Artifacts;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Agent.Correlation;
using PosAdminTool.Agent.Files;
using PosAdminTool.Agent.Operations;
using PosAdminTool.Application.Services;
using PosAdminTool.Contracts.V1.Artifacts;
using PosAdminTool.Contracts.V1.Backups;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Files;
using PosAdminTool.Contracts.V1.Operations;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Agent.Endpoints;

public static class BackupEndpoints
{
    public static void MapBackupEndpoints(this IEndpointRouteBuilder api)
    {
        var backups = api.MapGroup("/backups").RequireAuthorization(PolicyNames.LocalAdministratorsOnly);

        backups.MapGet("/options", async (IConfigurationService configuration, CancellationToken cancellationToken) =>
        {
            var settings = await configuration.LoadAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(new BackupOptionsDto(
                settings.BranchCode,
                ResolveBranchDatabase(settings),
                BackupService.ComponentDefinitions
                    .Select(component => new BackupComponentDto(component.Id, component.DisplayName))
                    .ToList()));
        })
        .WithName("GetBackupOptions")
        .Produces<BackupOptionsDto>();

        backups.MapGet(string.Empty, (HttpContext context, ArtifactCatalog catalog) =>
        {
            var principal = context.User.Identity?.Name ?? string.Empty;
            return Results.Ok(catalog.List(principal));
        })
        .WithName("ListBackups")
        .Produces<IReadOnlyList<ArtifactMetadataDto>>();

        backups.MapGet("/catalog", (HttpContext context, ArtifactCatalog catalog) =>
        {
            var principal = context.User.Identity?.Name ?? string.Empty;
            return Results.Ok(catalog.List(principal));
        })
        .WithName("ListBackupCatalog")
        .Produces<IReadOnlyList<ArtifactMetadataDto>>();

        backups.MapPost(string.Empty, async (
            CreateBackupRequestDto request,
            HttpContext context,
            IConfigurationService configuration,
            IFileHandleStore handleStore,
            IFileBrowseService browseService,
            BackupService backupService,
            OperationRegistry registry,
            CancellationToken cancellationToken) =>
        {
            var principal = context.User.Identity?.Name ?? string.Empty;
            var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? context.Request.Headers["Idempotency-Key"].ToString()
                : request.IdempotencyKey;
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { [nameof(request.IdempotencyKey)] = ["A non-empty idempotency key up to 128 characters is required."] },
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.ValidationFailed });
            }

            // Idempotent retries are read-only and must not consume the original one-use destination
            // handle a second time.
            if (registry.TryGetIdempotent(principal, idempotencyKey, out var duplicate))
            {
                return Results.Ok(duplicate);
            }

            var redemption = handleStore.Redeem(request.DestinationHandle, principal, FileHandlePurpose.BackupDestination);
            if (!redemption.Success || redemption.RootId is null || redemption.RelativeSubPath is null)
            {
                return Results.Problem(
                    title: "Backup destination handle rejected",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?>
                    {
                        [ProblemDetailsExtensionKeys.ErrorCode] = redemption.FailureErrorCode ?? ErrorCodes.BackupDestinationHandleInvalid,
                    });
            }

            ResolvedBrowseTarget target;
            try
            {
                target = browseService.ResolveForHandle(redemption.RootId, redemption.RelativeSubPath);
            }
            catch (FileBrowseValidationException ex)
            {
                return Results.Problem(
                    title: "Backup destination rejected",
                    statusCode: ex.StatusCode,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ex.ErrorCode });
            }

            if (!target.IsDirectory)
            {
                return Results.Problem(
                    title: "Backup destination rejected",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.BackupDestinationInvalid });
            }

            var settings = await configuration.LoadAsync(cancellationToken).ConfigureAwait(false);
            var componentIds = request.ComponentIds ?? [];
            var validation = backupService.Validate(settings, componentIds, target.CanonicalFullPath);
            if (!validation.Ready)
            {
                return Results.Problem(
                    title: "Backup preflight failed",
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    extensions: new Dictionary<string, object?>
                    {
                        [ProblemDetailsExtensionKeys.ErrorCode] = validation.Errors.FirstOrDefault()?.Code ?? ErrorCodes.BackupValidationFailed,
                        ["availableFreeSpaceBytes"] = validation.AvailableFreeSpaceBytes,
                        ["estimatedRequiredFreeSpaceBytes"] = validation.EstimatedRequiredFreeSpaceBytes,
                        ["errors"] = validation.Errors.Select(error => error.Message).ToList(),
                    });
            }

            var destinationReference = string.IsNullOrWhiteSpace(target.RelativeSubPath)
                ? target.RootId
                : string.Concat(target.RootId, " / ", target.RelativeSubPath);
            var workItem = new BackupOperationWorkItem(
                settings.Clone(),
                componentIds.ToList(),
                target.CanonicalFullPath,
                destinationReference);
            var correlation = CorrelationIdContext.TryGet(context) ?? string.Empty;

            return registry.TrySubmit(
                "backup",
                validation.BranchCode,
                principal,
                correlation,
                idempotencyKey,
                workItem,
                destinationReference,
                out var detail,
                out var duplicateSubmit)
                ? duplicateSubmit
                    ? Results.Ok(detail)
                    : Results.Accepted($"/api/v1/operations/{detail!.OperationId}", detail)
                : Results.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.OperationQueueFull });
        })
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithName("CreateBackup")
        .Produces<OperationDetailDto>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    private static string ResolveBranchDatabase(AppSettings settings) =>
        settings.Databases.FirstOrDefault(database => database.Contains("branch", StringComparison.OrdinalIgnoreCase))
        ?? "RmsBranchSrv";
}
