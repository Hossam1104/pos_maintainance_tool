using PosAdminTool.Agent.Antiforgery;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Application.UseCases;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Configuration;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Exceptions;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Agent.Endpoints;

public static class ConfigurationEndpoints
{
    public static void MapConfigurationEndpoints(this IEndpointRouteBuilder api)
    {
        var configuration = api.MapGroup("/configuration").RequireAuthorization(PolicyNames.LocalAdministratorsOnly);

        configuration.MapGet(string.Empty, async (AgentConfigurationUseCase useCase, CancellationToken cancellationToken) =>
        {
            var snapshot = await useCase.GetAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(ToDto(snapshot));
        })
        .WithName("GetConfiguration")
        .Produces<RedactedConfigurationDto>();

        // A mutation — antiforgery applies. SqlPassword/RdbPassword are write-only on the request and
        // never echoed back; a blank or omitted value means "keep the current secret" (plan section 5.5).
        configuration.MapPut(string.Empty, async (
            ConfigurationUpdateRequestDto request,
            AgentConfigurationUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var snapshot = await useCase.UpdateAsync(ToDomain(request), cancellationToken).ConfigureAwait(false);
                return Results.Ok(ToDto(snapshot));
            }
            catch (ConfigurationVersionConflictException ex)
            {
                return ToConflictProblem(ex);
            }
        })
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithName("UpdateConfiguration")
        .Produces<RedactedConfigurationDto>()
        .ProducesProblem(StatusCodes.Status409Conflict);

        // Clearing a secret is a distinct, explicitly authorized operation from replacing one (plan
        // section 5.5) — never a side effect of a blank field on PUT.
        configuration.MapPost("/secrets/clear", async (
            ClearSecretRequestDto request,
            AgentConfigurationUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var snapshot = await useCase.ClearSecretAsync(ToDomain(request.Secret), request.ExpectedVersion, cancellationToken).ConfigureAwait(false);
                return Results.Ok(ToDto(snapshot));
            }
            catch (ConfigurationVersionConflictException ex)
            {
                return ToConflictProblem(ex);
            }
        })
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithName("ClearConfigurationSecret")
        .Produces<RedactedConfigurationDto>()
        .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static RedactedConfigurationDto ToDto(AgentConfigurationSnapshot snapshot)
    {
        var config = snapshot.Configuration;
        return new RedactedConfigurationDto(
            config.SqlInstance,
            config.SqlUser,
            snapshot.HasSqlPassword,
            config.BranchCode,
            config.PosNumber,
            config.ApiBaseUrl,
            config.BackupFolder,
            config.Databases,
            config.Services,
            new RedactedDownloaderConfigurationDto(
                config.Downloader.ApiUrl,
                config.Downloader.RdbServerIp,
                config.Downloader.RdbUsername,
                snapshot.HasRdbPassword,
                config.Downloader.KnownBranchCodes,
                config.Downloader.PollIntervalSeconds,
                config.Downloader.TimeoutSeconds),
            config.Version);
    }

    private static AgentConfigurationUpdate ToDomain(ConfigurationUpdateRequestDto request) => new()
    {
        SqlInstance = request.SqlInstance,
        SqlUser = request.SqlUser,
        SqlPassword = request.SqlPassword,
        BranchCode = request.BranchCode,
        PosNumber = request.PosNumber,
        ApiBaseUrl = request.ApiBaseUrl,
        BackupFolder = request.BackupFolder,
        Databases = [.. request.Databases],
        Services = [.. request.Services],
        Downloader = new AgentDownloaderConfigurationUpdate
        {
            ApiUrl = request.Downloader.ApiUrl,
            RdbServerIp = request.Downloader.RdbServerIp,
            RdbUsername = request.Downloader.RdbUsername,
            RdbPassword = request.Downloader.RdbPassword,
            KnownBranchCodes = [.. request.Downloader.KnownBranchCodes],
            PollIntervalSeconds = request.Downloader.PollIntervalSeconds,
            TimeoutSeconds = request.Downloader.TimeoutSeconds
        },
        ExpectedVersion = request.ExpectedVersion
    };

    private static AgentSecretKind ToDomain(SecretKind kind) => kind switch
    {
        SecretKind.SqlPassword => AgentSecretKind.SqlPassword,
        SecretKind.RdbPassword => AgentSecretKind.RdbPassword,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static IResult ToConflictProblem(ConfigurationVersionConflictException ex) =>
        Results.Problem(
            title: "Configuration version conflict",
            detail: "The configuration was changed by another request. Reload and try again.",
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?>
            {
                [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.ConfigurationVersionConflict,
                ["expectedVersion"] = ex.ExpectedVersion,
                ["actualVersion"] = ex.ActualVersion,
            });
}
