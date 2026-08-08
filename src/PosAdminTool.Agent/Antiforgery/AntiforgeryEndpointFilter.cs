using Microsoft.AspNetCore.Antiforgery;
using PosAdminTool.Contracts.V1.Common;

namespace PosAdminTool.Agent.Antiforgery;

/// <summary>
/// Enforces the antiforgery double-submit-cookie token on a mutating endpoint (plan section 5.1,
/// section 6.1). The Angular shell reads the token from the non-HttpOnly antiforgery cookie (see
/// <c>/api/v1/antiforgery</c>) and mirrors it into the configured request header.
/// </summary>
public sealed class AntiforgeryEndpointFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                title: "Antiforgery validation failed",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?>
                {
                    [ProblemDetailsExtensionKeys.ErrorCode] = "antiforgery_validation_failed",
                });
        }

        return await next(context);
    }
}
