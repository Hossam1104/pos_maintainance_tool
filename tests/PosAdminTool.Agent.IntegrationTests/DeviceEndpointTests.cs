using System.Net;
using System.Net.Http.Json;
using PosAdminTool.Contracts.V1.Device;

namespace PosAdminTool.Agent.IntegrationTests;

public sealed class DeviceEndpointTests(AgentWebApplicationFactory factory) : IClassFixture<AgentWebApplicationFactory>
{
    [Theory]
    [InlineData("/api/v1/device/identity")]
    [InlineData("/api/v1/device/connectivity")]
    [InlineData("/api/v1/device/capabilities")]
    public async Task DeviceEndpoints_RejectUnauthenticatedRequests(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Capabilities_ReturnBrowseMetadataWithoutHostPaths()
    {
        var response = await factory.CreateAdminClient().GetAsync("/api/v1/device/capabilities");
        var body = await response.Content.ReadFromJsonAsync<DeviceCapabilitiesDto>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body!.BrowseRoots);
        Assert.Equal(AgentWebApplicationFactory.DefaultBrowseRootId, body.BrowseRoots[0].RootId);
        Assert.DoesNotContain(factory.FakeBrowseRootPath, await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
}
