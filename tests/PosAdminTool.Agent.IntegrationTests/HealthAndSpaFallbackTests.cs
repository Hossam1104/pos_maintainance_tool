using System.Net;

namespace PosAdminTool.Agent.IntegrationTests;

public class HealthAndSpaFallbackTests : IClassFixture<AgentWebApplicationFactory>
{
    private readonly AgentWebApplicationFactory _factory;

    public HealthAndSpaFallbackTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthLive_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Development_ExposesRuntimeOpenApiDocument()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"openapi\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Production_DoesNotExposeRuntimeOpenApiDocument()
    {
        using var productionFactory = new AgentWebApplicationFactory("Production");
        var client = productionFactory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("\"openapi\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RootRequest_ServesIndexHtml()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(AgentWebApplicationFactory.ResponseContainsIndexMarker(body));
    }

    [Fact]
    public async Task UnknownClientRoute_FallsBackToIndexHtml()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/services");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(AgentWebApplicationFactory.ResponseContainsIndexMarker(body));
    }
}
