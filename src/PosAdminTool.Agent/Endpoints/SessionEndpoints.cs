using PosAdminTool.Agent.Authorization;
using PosAdminTool.Contracts.V1.Session;

namespace PosAdminTool.Agent.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder api)
    {
        // Authenticated-only, not authorization-gated: a non-administrator still needs to see
        // IsAuthorized=false rather than a bare 403, so the shell can explain why every mutation is
        // disabled (plan section 5.2, section 7.3).
        api.MapGet("/session", (HttpContext httpContext, IAdministratorGroupChecker checker) =>
        {
            var isAuthorized = checker.IsInAdministratorsGroup(httpContext.User);
            var agentVersion = typeof(SessionEndpoints).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

            return Results.Ok(new SessionInfoDto(
                PrincipalName: httpContext.User.Identity?.Name ?? string.Empty,
                IsAuthorized: isAuthorized,
                AgentVersion: agentVersion,
                ApiVersion: ApiVersioning.CurrentVersion,
                SupportedApiVersions: ApiVersioning.SupportedVersions));
        })
        .RequireAuthorization()
        .WithName("GetSession")
        .Produces<SessionInfoDto>();
    }
}
