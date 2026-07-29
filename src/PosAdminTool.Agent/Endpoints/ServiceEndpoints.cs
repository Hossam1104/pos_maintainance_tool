using PosAdminTool.Agent.Antiforgery;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Agent.Correlation;
using PosAdminTool.Agent.Services;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Services;

namespace PosAdminTool.Agent.Endpoints;

public static class ServiceEndpoints
{
    public static void MapServiceEndpoints(this IEndpointRouteBuilder api)
    {
        var services = api.MapGroup("/services").RequireAuthorization(PolicyNames.LocalAdministratorsOnly);
        services.MapGet(string.Empty, async (ServiceMonitor monitor, CancellationToken cancellationToken) =>
            Results.Ok(await monitor.RefreshAsync(cancellationToken).ConfigureAwait(false)))
            .WithName("ListServices").Produces<IReadOnlyList<ServiceSummaryDto>>();

        services.MapPost("/{serviceId}/actions", async (string serviceId, ServiceActionRequestDto request, HttpContext context, ServiceCommandCoordinator commands, ServiceMonitor monitor, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 128 || !Enum.IsDefined(request.Action))
                return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.IdempotencyKey)] = ["An idempotency key up to 128 characters is required."], [nameof(request.Action)] = ["A supported service action is required."] }, extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.ValidationFailed });
            var principal = context.User.Identity?.Name ?? string.Empty;
            var correlation = CorrelationIdContext.TryGet(context) ?? string.Empty;
            var result = await commands.SubmitAsync(serviceId, request.Action, principal, correlation, request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
            var summary = result.ServiceId is null ? null : await monitor.GetAsync(result.ServiceId, cancellationToken).ConfigureAwait(false);
            return result.Status switch
            {
                ServiceCommandSubmitStatus.Accepted when summary is not null => Results.Accepted($"/api/v1/services/{serviceId}", summary),
                ServiceCommandSubmitStatus.Duplicate when summary is not null => Results.Ok(summary),
                ServiceCommandSubmitStatus.NotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound, extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.EntryNotFound }),
                ServiceCommandSubmitStatus.Conflict => Results.Problem(statusCode: StatusCodes.Status409Conflict, extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.OperationInvalidStateTransition }),
                _ => Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.OperationQueueFull }),
            };
        }).AddEndpointFilter<AntiforgeryEndpointFilter>().WithName("ControlService").Produces<ServiceSummaryDto>(202).ProducesProblem(404).ProducesProblem(409).ProducesProblem(429);
    }
}
