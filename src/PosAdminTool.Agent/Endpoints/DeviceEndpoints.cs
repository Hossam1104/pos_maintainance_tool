using Microsoft.Extensions.Options;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Agent.Device;
using PosAdminTool.Agent.Files;
using PosAdminTool.Contracts.V1.Device;

namespace PosAdminTool.Agent.Endpoints;

public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this IEndpointRouteBuilder api)
    {
        var device = api.MapGroup("/device").RequireAuthorization(PolicyNames.LocalAdministratorsOnly);
        device.MapGet("/identity", async (DeviceDiagnosticsService diagnostics, CancellationToken ct) =>
            Results.Ok(await diagnostics.GetIdentityAsync(ct).ConfigureAwait(false)))
            .WithName("GetDeviceIdentity").Produces<DeviceIdentityDto>();
        device.MapGet("/connectivity", async (DeviceDiagnosticsService diagnostics, CancellationToken ct) =>
            Results.Ok(await diagnostics.GetConnectivityAsync(ct).ConfigureAwait(false)))
            .WithName("GetDeviceConnectivity").Produces<DeviceConnectivityDto>();
        device.MapGet("/capabilities", (IOptions<FileBrowseOptions> browse) => Results.Ok(new DeviceCapabilitiesDto(
            typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            Environment.OSVersion.VersionString,
            browse.Value.Roots.Select(root => new BrowseRootDto(root.RootId, root.DisplayName)).ToList())))
            .WithName("GetDeviceCapabilities").Produces<DeviceCapabilitiesDto>();
    }
}
