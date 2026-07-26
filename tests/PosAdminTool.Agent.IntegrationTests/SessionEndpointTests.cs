using System.Net;
using System.Net.Http.Json;
using PosAdminTool.Contracts.V1.Session;

namespace PosAdminTool.Agent.IntegrationTests;

public class SessionEndpointTests : IClassFixture<AgentWebApplicationFactory>
{
    private readonly AgentWebApplicationFactory _factory;

    public SessionEndpointTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnauthenticatedRequest_IsRejected()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/session");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedNonAdministrator_ReceivesIsAuthorizedFalse()
    {
        var client = _factory.CreateNonAdminClient("TESTDOMAIN\\standard-user");

        var response = await client.GetAsync("/api/v1/session");
        var body = await response.Content.ReadFromJsonAsync<SessionInfoDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(body!.IsAuthorized);
        Assert.Equal("TESTDOMAIN\\standard-user", body.PrincipalName);
    }

    [Fact]
    public async Task AuthenticatedAdministrator_ReceivesIsAuthorizedTrue()
    {
        var client = _factory.CreateAdminClient("TESTDOMAIN\\admin-user");

        var response = await client.GetAsync("/api/v1/session");
        var body = await response.Content.ReadFromJsonAsync<SessionInfoDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.True(body!.IsAuthorized);
        Assert.Equal("TESTDOMAIN\\admin-user", body.PrincipalName);
        Assert.Equal("1.0", body.ApiVersion);
        Assert.Contains("1.0", body.SupportedApiVersions);
    }
}
