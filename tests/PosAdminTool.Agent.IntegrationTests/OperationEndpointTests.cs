using System.Net;
using System.Net.Http.Json;
using PosAdminTool.Contracts.V1.Operations;
using PosAdminTool.Contracts.V1.Session;

namespace PosAdminTool.Agent.IntegrationTests;

public sealed class OperationEndpointTests(AgentWebApplicationFactory factory) : IClassFixture<AgentWebApplicationFactory>
{
    [Fact]
    public async Task DuplicateIdempotencyKey_ReturnsTheSameOperationWithoutDuplicatingIt()
    {
        var client = await AdminAsync(); client.DefaultRequestHeaders.Add("Idempotency-Key", "test-idempotency-01");
        var first = await client.PostAsJsonAsync("/api/v1/operations", new SubmitOperationRequestDto("diagnostic", "B001"));
        var second = await client.PostAsJsonAsync("/api/v1/operations", new SubmitOperationRequestDto("diagnostic", "B001"));
        var one = await first.Content.ReadFromJsonAsync<OperationDetailDto>(TestSupport.TestJsonOptions.Default); var two = await second.Content.ReadFromJsonAsync<OperationDetailDto>(TestSupport.TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode); Assert.Equal(HttpStatusCode.OK, second.StatusCode); Assert.Equal(one!.OperationId, two!.OperationId);
    }

    [Fact]
    public async Task DestructiveDiagnostic_WritesExactlyOneSanitizedAuditRecord()
    {
        var client = await AdminAsync(); const string branch = "sentinel-branch-not-secret";
        var submit = await client.PostAsJsonAsync("/api/v1/operations", new SubmitOperationRequestDto("diagnostic-destructive", branch));
        var operation = await submit.Content.ReadFromJsonAsync<OperationDetailDto>(TestSupport.TestJsonOptions.Default);
        for (var attempt = 0; attempt < 30; attempt++) { await Task.Delay(50); var response = await client.GetAsync($"/api/v1/operations/{operation!.OperationId}"); var current = await response.Content.ReadFromJsonAsync<OperationDetailDto>(TestSupport.TestJsonOptions.Default); if (current!.State == OperationState.Succeeded) break; }
        var path = Path.Combine(factory.FakeConfigRootPath, "audit", "operations.jsonl"); var lines = await File.ReadAllLinesAsync(path);
        Assert.Single(lines); Assert.DoesNotContain("sentinel-sql-pw", lines[0], StringComparison.Ordinal); Assert.Contains(branch, lines[0], StringComparison.Ordinal);
    }

    private async Task<HttpClient> AdminAsync()
    {
        var client = factory.CreateAdminClient(); var token = await client.GetFromJsonAsync<AntiforgeryTokenDto>("/api/v1/antiforgery"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.Token); return client;
    }
}
