using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using PosAdminTool.Agent.Antiforgery;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Agent.Correlation;
using PosAdminTool.Agent.Operations;
using PosAdminTool.Agent.Restore;
using PosAdminTool.Application.Restore;
using PosAdminTool.Application.Services;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Operations;
using PosAdminTool.Contracts.V1.Restore;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;
using ApplicationRestoreMode = PosAdminTool.Application.Restore.RestoreMode;
using ContractRestoreMode = PosAdminTool.Contracts.V1.Restore.RestoreMode;

namespace PosAdminTool.Agent.Endpoints;

public static class RestoreEndpoints
{
    public static void MapRestoreEndpoints(this IEndpointRouteBuilder api)
    {
        var restores = api.MapGroup("/restores").RequireAuthorization(PolicyNames.LocalAdministratorsOnly);

        restores.MapPost("/uploads", async (
            HttpContext context,
            RestoreUploadStore uploads,
            RuntimeRetentionPolicy retention,
            CancellationToken cancellationToken) =>
        {
            var principal = context.User.Identity?.Name ?? string.Empty;
            try
            {
                if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } requestLimit)
                {
                    requestLimit.MaxRequestBodySize = checked(retention.MaxRestoreUploadBytes + 1);
                }

                var fileName = ResolveFileName(context.Request);
                RestoreUploadDto upload;
                if (context.Request.ContentType?.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase) == true)
                {
                    upload = await StageMultipartAsync(context, uploads, principal, fileName, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    upload = await uploads.StageAsync(principal, fileName, context.Request.Body, cancellationToken).ConfigureAwait(false);
                }

                return Results.Ok(upload);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (RestoreUploadTooLargeException)
            {
                return Results.Problem(
                    title: "Restore upload rejected",
                    statusCode: StatusCodes.Status413PayloadTooLarge,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.RestoreUploadTooLarge });
            }
            catch (RestoreUploadCapacityException)
            {
                return Results.Problem(
                    title: "Restore upload capacity reached",
                    statusCode: StatusCodes.Status429TooManyRequests,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.RestoreUploadCapacity });
            }
            catch (RestoreUploadInvalidException)
            {
                return Results.Problem(
                    title: "Restore upload rejected",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.RestoreUploadInvalid });
            }
            catch
            {
                return Results.Problem(
                    title: "Restore upload rejected",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.RestoreUploadInvalid });
            }
        })
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithName("UploadRestoreSource")
        .Produces<RestoreUploadDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        restores.MapPost("/preview", async (
            RestorePreviewRequestDto request,
            HttpContext context,
            IConfigurationService configuration,
            RestoreSourceResolver sourceResolver,
            RestoreService restoreService,
            RestoreChallengeStore challenges,
            RestoreUploadStore uploads,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var principal = context.User.Identity?.Name ?? string.Empty;
            RestoreSourceResolution resolution;
            try
            {
                resolution = sourceResolver.ResolvePreviewSource(request.Source, principal);
            }
            catch (RestoreSourceResolutionException ex)
            {
                return Results.Problem(
                    title: "Restore source rejected",
                    statusCode: ex.StatusCode,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ex.ErrorCode });
            }

            RestorePreviewBuildResult built;
            try
            {
                var settings = await configuration.LoadAsync(cancellationToken).ConfigureAwait(false);
                built = await restoreService.BuildPreviewAsync(settings, resolution.ToDescriptor(), ToApplicationMode(request.Mode), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                uploads.Release(resolution.Reference.UploadId);
                throw;
            }
            catch
            {
                uploads.Release(resolution.Reference.UploadId);
                return Results.Problem(
                    title: "Restore preview rejected",
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.RestorePreviewNotReady });
            }

            if (!built.Ready || built.Intent is null)
            {
                uploads.Release(resolution.Reference.UploadId);
                var rejected = MapPreview(string.Empty, request.Mode, built, timeProvider.GetUtcNow());
                return Results.Json(rejected, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            RestoreChallenge challenge;
            try
            {
                challenge = challenges.Issue(principal, built.Intent);
            }
            catch (RestoreChallengeCapacityException)
            {
                uploads.Release(resolution.Reference.UploadId);
                return Results.Problem(
                    title: "Restore challenge capacity reached",
                    statusCode: StatusCodes.Status429TooManyRequests,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.RestoreOperationQueueFull });
            }

            return Results.Ok(MapPreview(challenge.ChallengeId, request.Mode, built, challenge.ExpiresAtUtc));
        })
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithName("PreviewRestore")
        .Produces<RestorePreviewDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        restores.MapPost("/{previewId}/execute", async (
            string previewId,
            RestoreExecuteRequestDto request,
            HttpContext context,
            IConfigurationService configuration,
            RestoreSourceResolver sourceResolver,
            RestoreService restoreService,
            RestoreChallengeStore challenges,
            RestoreUploadStore uploads,
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

            if (!string.IsNullOrWhiteSpace(request.PreviewId)
                && !string.Equals(request.PreviewId, previewId, StringComparison.Ordinal))
            {
                return Results.Problem(
                    title: "Restore preview rejected",
                    statusCode: StatusCodes.Status409Conflict,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.RestoreChallengeChanged });
            }

            if (registry.TryGetIdempotent(principal, idempotencyKey, out var duplicate))
            {
                return Results.Ok(duplicate);
            }

            if (!challenges.TryGetIntent(previewId, principal, out var storedIntent, out var lookupError)
                || storedIntent is null)
            {
                return ToChallengeProblem(lookupError ?? ErrorCodes.RestoreChallengeNotFound);
            }

            RestoreSourceResolution resolution;
            try
            {
                resolution = sourceResolver.ResolveStoredSource(storedIntent.Source, principal);
            }
            catch (RestoreSourceResolutionException ex)
            {
                challenges.Invalidate(previewId, principal, ex.ErrorCode);
                uploads.Release(storedIntent.Source.UploadId);
                return Results.Problem(
                    title: "Restore source rejected",
                    statusCode: ex.StatusCode,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ex.ErrorCode });
            }

            RestorePreviewBuildResult recomputed;
            try
            {
                var settings = await configuration.LoadAsync(cancellationToken).ConfigureAwait(false);
                recomputed = await restoreService.BuildPreviewAsync(settings, resolution.ToDescriptor(), storedIntent.Mode, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                challenges.Invalidate(previewId, principal);
                throw;
            }
            catch
            {
                challenges.Invalidate(previewId, principal);
                uploads.Release(storedIntent.Source.UploadId);
                return Results.Problem(
                    title: "Restore preview is stale",
                    statusCode: StatusCodes.Status409Conflict,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.RestoreChallengeChanged });
            }

            if (!recomputed.Ready || recomputed.Intent is null)
            {
                challenges.Invalidate(previewId, principal);
                uploads.Release(storedIntent.Source.UploadId);
                return Results.Problem(
                    title: "Restore preview is stale",
                    statusCode: StatusCodes.Status409Conflict,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = recomputed.ErrorCode ?? ErrorCodes.RestoreChallengeChanged });
            }

            var redemption = challenges.Redeem(
                previewId,
                principal,
                recomputed.Intent.Fingerprint,
                request.TypedConfirmation ?? string.Empty);
            if (!redemption.Success || redemption.Intent is null)
            {
                return ToChallengeProblem(redemption.ErrorCode ?? ErrorCodes.RestoreChallengeChanged);
            }

            AppSettings currentSettings;
            try
            {
                currentSettings = await configuration.LoadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                uploads.Release(recomputed.Intent.Source.UploadId);
                throw;
            }
            catch
            {
                uploads.Release(recomputed.Intent.Source.UploadId);
                return Results.Problem(
                    title: "Restore operation could not be queued",
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.RestoreFailed });
            }
            var workItem = new RestoreOperationWorkItem(
                currentSettings.Clone(),
                recomputed.Intent.Source,
                recomputed.Intent.Mode,
                recomputed.Intent.Fingerprint,
                () => uploads.Release(recomputed.Intent.Source.UploadId));
            var correlation = CorrelationIdContext.TryGet(context) ?? string.Empty;
            var accepted = registry.TrySubmit(
                "restore",
                recomputed.Intent.BranchCode,
                principal,
                correlation,
                idempotencyKey,
                workItem,
                $"restore:{recomputed.Intent.Mode.ToString().ToLowerInvariant()}",
                ToAuditMode(recomputed.Intent.Mode),
                recomputed.Intent.TargetDatabase,
                out var detail,
                out var duplicateSubmit);
            if (!accepted)
            {
                workItem.Dispose();
                return Results.Problem(
                    title: "Restore operation queue is full",
                    statusCode: StatusCodes.Status429TooManyRequests,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.RestoreOperationQueueFull });
            }

            if (duplicateSubmit)
            {
                workItem.Dispose();
                return Results.Ok(detail);
            }

            return Results.Accepted($"/api/v1/operations/{detail!.OperationId}", detail);
        })
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithName("ExecuteRestore")
        .Produces<OperationDetailDto>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    private static async Task<RestoreUploadDto> StageMultipartAsync(
        HttpContext context,
        RestoreUploadStore uploads,
        string principal,
        string fallbackFileName,
        CancellationToken cancellationToken)
    {
        var contentType = context.Request.ContentType ?? string.Empty;
        var boundaryPart = contentType.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .FirstOrDefault(part => part.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase));
        if (boundaryPart is null) throw new RestoreUploadInvalidException();
        var boundary = boundaryPart["boundary=".Length..].Trim().Trim('"');
        if (boundary.Length == 0) throw new RestoreUploadInvalidException();

        var reader = new MultipartReader(boundary, context.Request.Body);
        MultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
                || !string.Equals(disposition.DispositionType, "form-data", StringComparison.OrdinalIgnoreCase)) continue;

            var fileName = !string.IsNullOrWhiteSpace(disposition.FileNameStar)
                ? disposition.FileNameStar
                : !string.IsNullOrWhiteSpace(disposition.FileName)
                    ? disposition.FileName
                    : fallbackFileName;
            return await uploads.StageAsync(principal, fileName ?? fallbackFileName, section.Body, cancellationToken).ConfigureAwait(false);
        }

        throw new RestoreUploadInvalidException();
    }

    private static string ResolveFileName(HttpRequest request)
    {
        var value = request.Headers["X-Restore-File-Name"].FirstOrDefault()
            ?? request.Headers["X-File-Name"].FirstOrDefault()
            ?? request.Query["fileName"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(value)) return value;

        if (ContentDispositionHeaderValue.TryParse(request.Headers.ContentDisposition, out var disposition))
        {
            return disposition.FileNameStar ?? disposition.FileName ?? "restore.zip";
        }

        return "restore.zip";
    }

    private static RestorePreviewDto MapPreview(
        string previewId,
        ContractRestoreMode mode,
        RestorePreviewBuildResult result,
        DateTimeOffset expiresAtUtc)
    {
        var intent = result.Intent;
        return new RestorePreviewDto(
            previewId,
            mode,
            intent?.TargetDatabase ?? string.Empty,
            intent?.LogicalFiles.Select(file => file.LogicalName).ToList() ?? [],
            intent?.ConfigTargets.Select(target => target.TargetId).ToList() ?? [],
            intent?.Services.Select(service => service.ServiceId).ToList() ?? [],
            intent?.RequiredFreeSpaceBytes ?? 0,
            result.Warnings,
            expiresAtUtc)
        {
            Ready = result.Ready && intent is not null,
            SqlMovePlan = intent?.SqlPlan.Moves
                .Select(move => new RestoreMovePlanDto(move.LogicalName, move.FileType, move.DestinationFileName))
                .ToList() ?? [],
            ConfirmationText = intent?.ConfirmationText ?? string.Empty,
            RejectionCode = result.ErrorCode,
            RejectionReason = result.SafeMessage,
        };
    }

    private static ApplicationRestoreMode ToApplicationMode(ContractRestoreMode mode) => mode switch
    {
        ContractRestoreMode.DatabaseOnly => ApplicationRestoreMode.DatabaseOnly,
        ContractRestoreMode.ConfigOnly => ApplicationRestoreMode.ConfigOnly,
        _ => ApplicationRestoreMode.Full,
    };

    private static string ToAuditMode(ApplicationRestoreMode mode) => mode switch
    {
        ApplicationRestoreMode.DatabaseOnly => "database-only",
        ApplicationRestoreMode.ConfigOnly => "config-only",
        _ => "full",
    };

    private static IResult ToChallengeProblem(string errorCode) =>
        Results.Problem(
            title: "Restore challenge rejected",
            statusCode: errorCode switch
            {
                ErrorCodes.RestoreChallengeExpired => StatusCodes.Status410Gone,
                ErrorCodes.RestoreChallengeUsed => StatusCodes.Status409Conflict,
                ErrorCodes.RestoreChallengeChanged => StatusCodes.Status409Conflict,
                ErrorCodes.RestoreConfirmationMismatch => StatusCodes.Status400BadRequest,
                ErrorCodes.RestoreChallengeWrongPrincipal => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            },
            extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = errorCode });
}
