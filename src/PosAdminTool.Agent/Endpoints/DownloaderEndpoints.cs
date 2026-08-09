using PosAdminTool.Agent.Antiforgery;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Agent.Correlation;
using PosAdminTool.Agent.Operations;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Downloader;
using PosAdminTool.Contracts.V1.Operations;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;
using PosAdminTool.Infrastructure.Http;
using PosAdminTool.Infrastructure.Smb;

namespace PosAdminTool.Agent.Endpoints;

/// <summary>Server-owned entry point for Support Hub downloader integration.</summary>
public static class DownloaderEndpoints
{
    public static void MapDownloaderEndpoints(this IEndpointRouteBuilder api)
    {
        var downloads = api.MapGroup("/downloads").RequireAuthorization(PolicyNames.LocalAdministratorsOnly);

        downloads.MapPost("/batches", async (
            TriggerBatchRequestDto request,
            HttpContext context,
            IAgentConfigurationStore configurations,
            OperationRegistry registry,
            CancellationToken cancellationToken) =>
        {
            var principal = context.User.Identity?.Name ?? string.Empty;
            var idempotencyKey = string.IsNullOrWhiteSpace(request?.IdempotencyKey)
                ? context.Request.Headers["Idempotency-Key"].ToString()
                : request.IdempotencyKey;
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { [nameof(request.IdempotencyKey)] = ["A non-empty idempotency key up to 128 characters is required."] },
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.ValidationFailed });
            }

            // Idempotent retries are read-only and do not reload credentials or revalidate an
            // already accepted request in a way that could create a second operation.
            if (registry.TryGetIdempotent(principal, idempotencyKey, out var duplicate))
            {
                return Results.Ok(duplicate);
            }

            IReadOnlyList<string> branches;
            try
            {
                branches = DownloaderInputPolicy.NormalizeBranchCodes(request?.BranchCodes);
            }
            catch (ArgumentException)
            {
                return Results.Problem(
                    title: "Downloader request rejected",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = DownloaderFailureCodes.InvalidBranch });
            }

            var configuration = await configurations.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!TryValidateServerOwnedConfiguration(configuration.Downloader, out var configurationCode))
            {
                return Results.Problem(
                    title: "Downloader configuration rejected",
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = configurationCode });
            }

            var workItem = new DownloaderOperationWorkItem(configuration.Downloader.Clone(), branches);
            var correlation = CorrelationIdContext.TryGet(context) ?? string.Empty;
            var branchSnapshot = string.Join(',', branches);
            return registry.TrySubmit(
                "downloader",
                branchSnapshot,
                principal,
                correlation,
                idempotencyKey,
                workItem,
                destinationReference: null,
                operationMode: "batch",
                operationTarget: "rdb-backup",
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
        .WithName("CreateDownloaderBatch")
        .Produces<OperationDetailDto>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    private static bool TryValidateServerOwnedConfiguration(
        AgentDownloaderConfiguration configuration,
        out string failureCode)
    {
        failureCode = DownloaderFailureCodes.InvalidConfiguration;
        try
        {
            if (string.IsNullOrWhiteSpace(configuration.ApiUrl))
            {
                failureCode = DownloaderFailureCodes.EndpointRejected;
                return false;
            }

            _ = BackupApiEndpointPolicy.FromConfiguredEndpoint(configuration.ApiUrl);
            _ = SmbPathResolver.ValidateServerAddress(configuration.RdbServerIp);
            _ = SmbPathResolver.ValidateCanonicalRoot(configuration.BackupRootFolder);
            DownloaderInputPolicy.ValidateSettings(new DbDownloaderSettings
            {
                ApiUrl = configuration.ApiUrl,
                RdbServerIp = configuration.RdbServerIp,
                RdbUsername = configuration.RdbUsername,
                BackupRootFolder = configuration.BackupRootFolder,
                PollIntervalSeconds = configuration.PollIntervalSeconds,
                TimeoutSeconds = configuration.TimeoutSeconds,
                StableSizeObservationAttempts = configuration.StableSizeObservationAttempts,
                StableSizeObservationIntervalSeconds = configuration.StableSizeObservationIntervalSeconds
            });
            return true;
        }
        catch (BackupApiPolicyException)
        {
            failureCode = DownloaderFailureCodes.EndpointRejected;
            return false;
        }
        catch (SmbPathPolicyException)
        {
            failureCode = DownloaderFailureCodes.SmbRootRejected;
            return false;
        }
        catch (ArgumentException)
        {
            failureCode = DownloaderFailureCodes.InvalidConfiguration;
            return false;
        }
    }
}
