using PosAdminTool.Agent.Antiforgery;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Agent.Correlation;
using PosAdminTool.Agent.Maintenance;
using PosAdminTool.Agent.Operations;
using PosAdminTool.Application.Maintenance;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Maintenance;
using PosAdminTool.Contracts.V1.Operations;
using PosAdminTool.Domain.Interfaces;

namespace PosAdminTool.Agent.Endpoints;

public static class MaintenanceEndpoints
{
    public static void MapMaintenanceEndpoints(this IEndpointRouteBuilder api)
    {
        var maintenance = api.MapGroup("/maintenance").RequireAuthorization(PolicyNames.LocalAdministratorsOnly);

        maintenance.MapPost("/cleanup/preview", async (
            HttpContext context,
            IConfigurationService configuration,
            MaintenanceService service,
            MaintenanceChallengeStore challenges,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var principal = context.User.Identity?.Name ?? string.Empty;
            try
            {
                var settings = await configuration.LoadAsync(cancellationToken).ConfigureAwait(false);
                var built = await service.BuildCleanupPreviewAsync(settings, cancellationToken).ConfigureAwait(false);
                if (!built.Ready || built.Intent is null)
                {
                    return Results.Json(MapCleanupPreview(built, string.Empty, timeProvider.GetUtcNow()), statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                MaintenanceChallenge challenge;
                try
                {
                    challenge = challenges.Issue(principal, built.Intent);
                }
                catch (MaintenanceChallengeCapacityException)
                {
                    return Results.Problem(
                        title: "Maintenance challenge capacity reached",
                        statusCode: StatusCodes.Status429TooManyRequests,
                        extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.MaintenanceOperationQueueFull });
                }

                return Results.Ok(MapCleanupPreview(built, challenge.ChallengeId, challenge.ExpiresAtUtc));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return Results.Problem(
                    title: "Cleanup preview rejected",
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.MaintenancePreviewNotReady });
            }
        })
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithName("PreviewMaintenanceCleanup")
        .Produces<CleanupPreviewDto>()
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        maintenance.MapPost("/cleanup/execute", async (
            CleanupExecuteRequestDto request,
            HttpContext context,
            IConfigurationService configuration,
            MaintenanceService service,
            MaintenanceChallengeStore challenges,
            OperationRegistry registry,
            CancellationToken cancellationToken) =>
        {
            var principal = context.User.Identity?.Name ?? string.Empty;
            var idempotencyKey = ResolveIdempotencyKey(request.IdempotencyKey, context);
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A non-empty idempotency key up to 128 characters is required."] },
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.ValidationFailed });
            }

            if (registry.TryGetIdempotent(principal, idempotencyKey, out var duplicate)) return Results.Ok(duplicate);

            if (!challenges.TryGetIntent(request.ChallengeId, principal, out var storedIntent, out var lookupError)
                || storedIntent is null
                || storedIntent.Mode != MaintenanceMode.Cleanup)
            {
                return ToChallengeProblem(lookupError ?? ErrorCodes.MaintenanceChallengeChanged);
            }

            CleanupPreviewBuildResult recomputed;
            try
            {
                var settings = await configuration.LoadAsync(cancellationToken).ConfigureAwait(false);
                recomputed = await service.BuildCleanupPreviewAsync(settings, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                challenges.Invalidate(request.ChallengeId, principal);
                return Results.Problem(
                    title: "Cleanup preview changed",
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.MaintenancePreviewNotReady });
            }

            if (!recomputed.Ready
                || recomputed.Intent is null
                || !string.Equals(storedIntent.Fingerprint, recomputed.Intent.Fingerprint, StringComparison.Ordinal))
            {
                challenges.Invalidate(request.ChallengeId, principal);
                return ToChallengeProblem(ErrorCodes.MaintenanceChallengeChanged);
            }

            var redemption = challenges.Redeem(
                request.ChallengeId,
                principal,
                recomputed.Intent.Fingerprint,
                request.TypedConfirmation ?? string.Empty);
            if (!redemption.Success || redemption.Intent is null)
            {
                return ToChallengeProblem(redemption.ErrorCode ?? ErrorCodes.MaintenanceChallengeChanged);
            }

            var workItem = new MaintenanceOperationWorkItem(MaintenanceMode.Cleanup, redemption.Intent.Fingerprint);
            var correlation = CorrelationIdContext.TryGet(context) ?? string.Empty;
            if (!registry.TrySubmit(
                    "cleanup",
                    redemption.Intent.BranchCode,
                    principal,
                    correlation,
                    idempotencyKey,
                    workItem,
                    null,
                    "cleanup",
                    "configured-targets",
                    out var detail,
                    out var duplicateSubmit))
            {
                workItem.Dispose();
                return Results.Problem(
                    title: "Maintenance operation queue is full",
                    statusCode: StatusCodes.Status429TooManyRequests,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.MaintenanceOperationQueueFull });
            }

            if (duplicateSubmit)
            {
                workItem.Dispose();
                return Results.Ok(detail);
            }

            return Results.Accepted($"/api/v1/operations/{detail!.OperationId}", detail);
        })
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithName("ExecuteMaintenanceCleanup")
        .Produces<OperationDetailDto>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        maintenance.MapPost("/reset/preview", async (
            HttpContext context,
            IConfigurationService configuration,
            MaintenanceService service,
            MaintenanceChallengeStore challenges,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var principal = context.User.Identity?.Name ?? string.Empty;
            try
            {
                var settings = await configuration.LoadAsync(cancellationToken).ConfigureAwait(false);
                var built = await service.BuildBranchResetPreviewAsync(settings, cancellationToken).ConfigureAwait(false);
                if (!built.Ready || built.Intent is null)
                {
                    return Results.Json(MapBranchResetPreview(built, string.Empty, timeProvider.GetUtcNow()), statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                MaintenanceChallenge challenge;
                try
                {
                    challenge = challenges.Issue(principal, built.Intent);
                }
                catch (MaintenanceChallengeCapacityException)
                {
                    return Results.Problem(
                        title: "Maintenance challenge capacity reached",
                        statusCode: StatusCodes.Status429TooManyRequests,
                        extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.MaintenanceOperationQueueFull });
                }

                return Results.Ok(MapBranchResetPreview(built, challenge.ChallengeId, challenge.ExpiresAtUtc));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return Results.Problem(
                    title: "Branch reset preview rejected",
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.MaintenancePreviewNotReady });
            }
        })
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithName("PreviewMaintenanceBranchReset")
        .Produces<BranchResetPreviewDto>()
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        maintenance.MapPost("/reset/execute", async (
            BranchResetExecuteRequestDto request,
            HttpContext context,
            IConfigurationService configuration,
            MaintenanceService service,
            MaintenanceChallengeStore challenges,
            OperationRegistry registry,
            CancellationToken cancellationToken) =>
        {
            var principal = context.User.Identity?.Name ?? string.Empty;
            var idempotencyKey = ResolveIdempotencyKey(request.IdempotencyKey, context);
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A non-empty idempotency key up to 128 characters is required."] },
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.ValidationFailed });
            }

            if (registry.TryGetIdempotent(principal, idempotencyKey, out var duplicate)) return Results.Ok(duplicate);

            if (!challenges.TryGetIntent(request.ChallengeId, principal, out var storedIntent, out var lookupError)
                || storedIntent is null
                || storedIntent.Mode != MaintenanceMode.BranchReset)
            {
                return ToChallengeProblem(lookupError ?? ErrorCodes.MaintenanceChallengeChanged);
            }

            BranchResetPreviewBuildResult recomputed;
            try
            {
                var settings = await configuration.LoadAsync(cancellationToken).ConfigureAwait(false);
                recomputed = await service.BuildBranchResetPreviewAsync(settings, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                challenges.Invalidate(request.ChallengeId, principal);
                return Results.Problem(
                    title: "Branch reset preview changed",
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.MaintenancePreviewNotReady });
            }

            if (!recomputed.Ready
                || recomputed.Intent is null
                || !string.Equals(storedIntent.Fingerprint, recomputed.Intent.Fingerprint, StringComparison.Ordinal))
            {
                challenges.Invalidate(request.ChallengeId, principal);
                return ToChallengeProblem(ErrorCodes.MaintenanceChallengeChanged);
            }

            var redemption = challenges.Redeem(
                request.ChallengeId,
                principal,
                recomputed.Intent.Fingerprint,
                request.TypedConfirmation ?? string.Empty);
            if (!redemption.Success || redemption.Intent is null)
            {
                return ToChallengeProblem(redemption.ErrorCode ?? ErrorCodes.MaintenanceChallengeChanged);
            }

            var workItem = new MaintenanceOperationWorkItem(MaintenanceMode.BranchReset, redemption.Intent.Fingerprint);
            var correlation = CorrelationIdContext.TryGet(context) ?? string.Empty;
            if (!registry.TrySubmit(
                    "branch-reset",
                    redemption.Intent.BranchCode,
                    principal,
                    correlation,
                    idempotencyKey,
                    workItem,
                    null,
                    "branch-reset",
                    redemption.Intent.DatabaseName,
                    out var detail,
                    out var duplicateSubmit))
            {
                workItem.Dispose();
                return Results.Problem(
                    title: "Maintenance operation queue is full",
                    statusCode: StatusCodes.Status429TooManyRequests,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.MaintenanceOperationQueueFull });
            }

            if (duplicateSubmit)
            {
                workItem.Dispose();
                return Results.Ok(detail);
            }

            return Results.Accepted($"/api/v1/operations/{detail!.OperationId}", detail);
        })
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithName("ExecuteMaintenanceBranchReset")
        .Produces<OperationDetailDto>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    private static string ResolveIdempotencyKey(string? bodyKey, HttpContext context) =>
        string.IsNullOrWhiteSpace(bodyKey)
            ? context.Request.Headers["Idempotency-Key"].ToString()
            : bodyKey.Trim();

    private static CleanupPreviewDto MapCleanupPreview(
        CleanupPreviewBuildResult result,
        string challengeId,
        DateTimeOffset expiresAtUtc) => new(
            challengeId,
            result.Services,
            result.Targets.Where(target => target.Accepted).Select(target => target.TargetId).ToList(),
            result.Intent?.ConfirmationText ?? string.Empty,
            expiresAtUtc)
        {
            Ready = result.Ready,
            Targets = result.Targets.Select(target => new CleanupTargetPreviewDto(
                target.TargetId,
                target.Accepted,
                target.Exists,
                target.IsDirectory,
                target.LengthBytes,
                target.ChildCount,
                target.RejectionCode)).ToList(),
            Rejections = result.Rejections.Select(item => new MaintenancePolicyRejectionDto(item.TargetId, item.Code, item.Message)).ToList(),
            Warnings = result.Warnings,
            AvailableFreeSpaceBytes = result.AvailableFreeSpaceBytes,
        };

    private static BranchResetPreviewDto MapBranchResetPreview(
        BranchResetPreviewBuildResult result,
        string challengeId,
        DateTimeOffset expiresAtUtc) => new(
            challengeId,
            result.Intent?.BranchCode ?? string.Empty,
            result.Tables.Select(table => table.TableName).ToList(),
            result.Intent?.ConfirmationText ?? string.Empty,
            expiresAtUtc)
        {
            Ready = result.Ready,
            DatabaseName = result.Intent?.DatabaseName ?? string.Empty,
            TableScopes = result.Tables.Select(table => new BranchResetTablePreviewDto(table.TableName, table.MatchingRows)).ToList(),
            Rejections = result.Rejections.Select(item => new MaintenancePolicyRejectionDto(item.TargetId, item.Code, item.Message)).ToList(),
            Warnings = result.Warnings,
            AvailableFreeSpaceBytes = result.AvailableFreeSpaceBytes,
        };

    internal static IResult ToChallengeProblem(string errorCode) =>
        Results.Problem(
            title: "Maintenance challenge rejected",
            statusCode: errorCode switch
            {
                ErrorCodes.MaintenanceChallengeExpired => StatusCodes.Status410Gone,
                ErrorCodes.MaintenanceChallengeUsed => StatusCodes.Status409Conflict,
                ErrorCodes.MaintenanceChallengeChanged => StatusCodes.Status409Conflict,
                ErrorCodes.MaintenanceChallengeWrongPrincipal => StatusCodes.Status404NotFound,
                ErrorCodes.MaintenanceChallengeNotFound => StatusCodes.Status404NotFound,
                ErrorCodes.MaintenanceConfirmationMismatch => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest,
            },
            extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = errorCode });
}
