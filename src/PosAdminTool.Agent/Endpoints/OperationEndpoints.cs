using PosAdminTool.Agent.Antiforgery;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Agent.Correlation;
using PosAdminTool.Agent.Operations;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Operations;

namespace PosAdminTool.Agent.Endpoints;
public static class OperationEndpoints
{
    public static void MapOperationEndpoints(this IEndpointRouteBuilder api)
    {
        var operations = api.MapGroup("/operations").RequireAuthorization(PolicyNames.LocalAdministratorsOnly);
        operations.MapPost(string.Empty, (SubmitOperationRequestDto request, HttpContext context, OperationRegistry registry, IHostEnvironment env) =>
        {
            if (!env.IsDevelopment() || request.OperationType is not ("diagnostic" or "diagnostic-destructive")) return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.OperationUnsupported });
            var principal = context.User.Identity?.Name ?? string.Empty;
            var correlation = CorrelationIdContext.TryGet(context) ?? string.Empty;
            var key = context.Request.Headers["Idempotency-Key"].ToString();
            if (key.Length > 128)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["Idempotency-Key"] = ["The idempotency key must be 128 characters or fewer."] },
                    extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.ValidationFailed });
            }

            return registry.TrySubmit(request.OperationType, request.BranchCodeSnapshot, principal, correlation, key, out var detail, out var duplicate) ? (duplicate ? Results.Ok(detail) : Results.Accepted($"/api/v1/operations/{detail!.OperationId}", detail)) : Results.Problem(statusCode: 429, extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.OperationQueueFull });
        }).AddEndpointFilter<AntiforgeryEndpointFilter>().WithName("SubmitOperation").Produces<OperationDetailDto>(202).ProducesProblem(400).ProducesProblem(429);
        operations.MapGet(string.Empty, (OperationRegistry registry) => Results.Ok(registry.List())).WithName("ListOperations").Produces<IReadOnlyList<OperationSummaryDto>>();
        operations.MapGet("/{id}", (string id, OperationRegistry registry) => registry.TryGet(id, out var detail) ? Results.Ok(detail) : NotFound()).WithName("GetOperation").Produces<OperationDetailDto>().ProducesProblem(404);
        operations.MapPost("/{id}/cancel", (string id, OperationRegistry registry) => registry.Cancel(id, out var detail) ? Results.Ok(new CancelOperationResponseDto(id, detail!.State)) : NotFound()).AddEndpointFilter<AntiforgeryEndpointFilter>().WithName("CancelOperation").Produces<CancelOperationResponseDto>().ProducesProblem(404);
    }
    private static IResult NotFound() => Results.Problem(statusCode: 404, extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.OperationNotFound });
}
