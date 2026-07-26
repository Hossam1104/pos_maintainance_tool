using Microsoft.AspNetCore.Antiforgery;
using PosAdminTool.Contracts.V1.Session;

namespace PosAdminTool.Agent.Endpoints;

public static class AntiforgeryEndpoints
{
    /// <summary>
    /// Issues the antiforgery cookie and returns its matching request token. The Angular shell
    /// calls this once per session and mirrors the token into the configured request header on
    /// every mutation (double-submit-cookie pattern; plan section 5.1/6.1).
    /// </summary>
    public static void MapAntiforgeryEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/antiforgery", (HttpContext httpContext, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new AntiforgeryTokenDto(tokens.RequestToken!));
        })
        .RequireAuthorization()
        .WithName("GetAntiforgeryToken")
        .Produces<AntiforgeryTokenDto>();
    }
}
