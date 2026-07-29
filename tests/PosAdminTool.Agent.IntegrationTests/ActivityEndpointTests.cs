using System.Net;
using System.Net.Http.Json;
using PosAdminTool.Contracts.V1.Activity;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Operations;
using PosAdminTool.Contracts.V1.Session;

namespace PosAdminTool.Agent.IntegrationTests;

public sealed class ActivityEndpointTests(AgentWebApplicationFactory factory) : IClassFixture<AgentWebApplicationFactory>
{
    [Fact]
    public async Task RecentActivity_RejectsUnauthenticatedRequests()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/activity");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RecentActivity_ReturnsRedactedAgentOperation()
    {
        var client = factory.CreateAdminClient();
        var token = await client.GetFromJsonAsync<AntiforgeryTokenDto>("/api/v1/antiforgery");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.Token);
        var submit = await client.PostAsJsonAsync("/api/v1/operations", new SubmitOperationRequestDto("diagnostic", "B001"));

        var activity = await client.GetFromJsonAsync<PagedResultDto<ActivityRecordDto>>("/api/v1/activity");

        Assert.Equal(HttpStatusCode.Accepted, submit.StatusCode);
        Assert.NotNull(activity);
        Assert.Contains(activity!.Items, item => item.Category == "operation" && item.Summary.Contains("diagnostic", StringComparison.Ordinal));
        Assert.DoesNotContain("B001", System.Text.Json.JsonSerializer.Serialize(activity.Items), StringComparison.Ordinal);
    }
}
