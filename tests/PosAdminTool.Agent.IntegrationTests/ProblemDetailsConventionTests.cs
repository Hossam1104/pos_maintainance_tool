using System.Net;
using System.Net.Http.Json;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Files;

namespace PosAdminTool.Agent.IntegrationTests;

public class ProblemDetailsConventionTests : IClassFixture<AgentWebApplicationFactory>
{
    private readonly AgentWebApplicationFactory _factory;

    public ProblemDetailsConventionTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RejectedFileBrowseRequest_ReturnsProblemDetailsWithCorrelationId()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync("/api/v1/files/browse", new FileBrowseRequestDto("unknown-root", string.Empty));
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problem);
        Assert.True(problem!.ContainsKey(ProblemDetailsExtensionKeys.CorrelationId));
        Assert.True(problem.ContainsKey(ProblemDetailsExtensionKeys.ErrorCode));
        Assert.False(string.IsNullOrWhiteSpace(problem[ProblemDetailsExtensionKeys.CorrelationId].ToString()));

        // The correlation ID on the response header must match the one embedded in the body.
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var headerValues));
        Assert.Equal(headerValues!.Single(), problem[ProblemDetailsExtensionKeys.CorrelationId].ToString());
    }

    [Fact]
    public async Task ResponseCarriesTheClientSuppliedCorrelationIdWhenProvided()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", "client-supplied-correlation-id");

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        Assert.Equal("client-supplied-correlation-id", values!.Single());
    }
}
