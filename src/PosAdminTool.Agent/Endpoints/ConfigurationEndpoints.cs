using PosAdminTool.Agent.Antiforgery;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Application.UseCases;
using PosAdminTool.Application.Services;
using PosAdminTool.Agent.Device;
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
            if (TryValidate(request, out var validationProblem))
            {
                return validationProblem;
            }

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
            var isKnownSecretKind = TryGetSecretKind(request.Secret, out var kind);
            if (request.ExpectedVersion < 1 || !isKnownSecretKind)
            {
                var errors = new Dictionary<string, string[]>();
                if (request.ExpectedVersion < 1) errors[nameof(request.ExpectedVersion)] = ["Expected version must be positive."];
                if (!isKnownSecretKind) errors[nameof(request.Secret)] = ["Unknown secret kind."];
                return Results.ValidationProblem(
                    errors,
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.ValidationFailed });
            }

            try
            {
                var snapshot = await useCase.ClearSecretAsync(kind, request.ExpectedVersion, cancellationToken).ConfigureAwait(false);
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

        configuration.MapPost("/import-rms", async (ImportFromRmsUseCase import, AgentConfigurationUseCase useCase, CancellationToken cancellationToken) =>
        {
            var result = await import.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return result.Status == PosAdminTool.Domain.Enums.OperationStatus.Success
                ? Results.Ok(ToDto(await useCase.GetAsync(cancellationToken).ConfigureAwait(false)))
                : Results.Problem(title: "RMS import failed", detail: "The Agent could not import RMS configuration.", statusCode: StatusCodes.Status422UnprocessableEntity);
        })
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithName("ImportConfigurationFromRms")
        .Produces<RedactedConfigurationDto>()
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        configuration.MapPost("/test-database", async (DeviceDiagnosticsService diagnostics, CancellationToken cancellationToken) =>
            Results.Ok(new DiagnosticResultDto(await diagnostics.TestDatabaseAsync(cancellationToken).ConfigureAwait(false))))
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithName("TestConfigurationDatabase")
        .Produces<DiagnosticResultDto>();

        configuration.MapPost("/verify-branch", async (DeviceDiagnosticsService diagnostics, CancellationToken cancellationToken) =>
            Results.Ok(new DiagnosticResultDto(await diagnostics.VerifyBranchAsync(cancellationToken).ConfigureAwait(false))))
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithName("VerifyConfigurationBranch")
        .Produces<DiagnosticResultDto>();
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
            config.Release,
            config.ClientName,
            config.ApiBaseUrl,
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
        Release = request.Release,
        ClientName = request.ClientName,
        ApiBaseUrl = request.ApiBaseUrl,
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

    private static bool TryValidate(ConfigurationUpdateRequestDto request, out IResult problem)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ExpectedVersion < 1) errors[nameof(request.ExpectedVersion)] = ["Expected version must be positive."];
        if (request.Downloader is null) errors[nameof(request.Downloader)] = ["Downloader settings are required."];
        if (request.BranchCode.Length > 50 || request.PosNumber.Length > 50) errors["identity"] = ["Branch and POS values must be 50 characters or fewer."];
        if (request.ApiBaseUrl.Length > 0 && !Uri.TryCreate(request.ApiBaseUrl, UriKind.Absolute, out _)) errors[nameof(request.ApiBaseUrl)] = ["API URL must be absolute."];
        if (request.Databases.Count > 50 || request.Services.Count > 50) errors["lists"] = ["Too many configured entries."];
        if (request.Downloader is not null)
        {
            if (request.Downloader.ApiUrl.Length > 0 && !Uri.TryCreate(request.Downloader.ApiUrl, UriKind.Absolute, out _)) errors[nameof(request.Downloader.ApiUrl)] = ["Downloader API URL must be absolute."];
            if (request.Downloader.PollIntervalSeconds is < 1 or > 3600 || request.Downloader.TimeoutSeconds is < 1 or > 86400) errors["downloader"] = ["Downloader intervals are outside the permitted range."];
            if (request.Downloader.KnownBranchCodes.Count > 50) errors[nameof(request.Downloader.KnownBranchCodes)] = ["Too many known branch codes."];
        }
        problem = errors.Count == 0
            ? Results.Empty
            : Results.ValidationProblem(errors, extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.ValidationFailed });
        return errors.Count > 0;
    }

    private static bool TryGetSecretKind(SecretKind kind, out AgentSecretKind agentKind)
    {
        agentKind = kind switch
        {
            SecretKind.SqlPassword => AgentSecretKind.SqlPassword,
            SecretKind.RdbPassword => AgentSecretKind.RdbPassword,
            _ => default,
        };

        return kind is SecretKind.SqlPassword or SecretKind.RdbPassword;
    }

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
