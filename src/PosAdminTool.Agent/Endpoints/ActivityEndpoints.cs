using PosAdminTool.Agent.Authorization;
using PosAdminTool.Agent.Operations;
using PosAdminTool.Contracts.V1.Activity;
using PosAdminTool.Contracts.V1.Common;

namespace PosAdminTool.Agent.Endpoints;

/// <summary>Returns the Agent's in-memory, redacted recent-operation timeline for the overview.</summary>
public static class ActivityEndpoints
{
    public static void MapActivityEndpoints(this IEndpointRouteBuilder api)
    {
        var activity = api.MapGroup("/activity").RequireAuthorization(PolicyNames.LocalAdministratorsOnly);
        activity.MapGet(string.Empty, (OperationRegistry registry) =>
        {
            const int pageSize = 5;
            var all = registry.ListActivity();
            return Results.Ok(new PagedResultDto<ActivityRecordDto>(all.Take(pageSize).ToList(), 1, pageSize, all.Count));
        })
        .WithName("ListRecentActivity")
        .Produces<PagedResultDto<ActivityRecordDto>>();
    }
}
