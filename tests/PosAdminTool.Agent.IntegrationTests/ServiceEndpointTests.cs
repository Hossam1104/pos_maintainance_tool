using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PosAdminTool.Contracts.V1.Services;
using PosAdminTool.Contracts.V1.Session;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;
using PosAdminTool.Agent.IntegrationTests.TestSupport;

namespace PosAdminTool.Agent.IntegrationTests;

public sealed class ServiceEndpointTests(AgentWebApplicationFactory factory) : IClassFixture<AgentWebApplicationFactory>, IDisposable
{
    [Fact]
    public async Task Endpoints_RejectUnauthenticatedRequests_AndNeverAcceptRawServiceNames()
    {
        await ConfigureAsync("sentinel-service");
        var anonymous = factory.CreateClient();
        var get = await anonymous.GetAsync("/api/v1/services");
        var post = await anonymous.PostAsJsonAsync("/api/v1/services/sentinel-service/actions", new ServiceActionRequestDto(ServiceActionKind.Start, "anonymous"));

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);

        var client = await AdminAsync();
        var rawName = await client.PostAsJsonAsync("/api/v1/services/sentinel-service/actions", new ServiceActionRequestDto(ServiceActionKind.Start, "raw-name"));
        Assert.Equal(HttpStatusCode.NotFound, rawName.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsConfiguredOpaqueIds_StatusEvidence_AndAllowedActions()
    {
        await ConfigureAsync("running-service", "stopped-service", "missing-service");
        factory.ServiceManager.SetStatus("running-service", ServiceStatus.Running);
        factory.ServiceManager.SetStatus("stopped-service", ServiceStatus.Stopped);

        var response = await (await AdminAsync()).GetAsync("/api/v1/services");
        var services = await response.Content.ReadFromJsonAsync<List<ServiceSummaryDto>>(TestJsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(services);
        Assert.All(services!, service => Assert.StartsWith("svc-", service.ServiceId));
        Assert.DoesNotContain(services!, service => service.ServiceId.Contains("service", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(services!, service => service.DisplayName == "running-service" && service.State == ServiceRuntimeState.Running && service.AllowedActions.SequenceEqual([ServiceActionKind.Stop, ServiceActionKind.Restart]) && service.LastChecked.LastCheckedUtc is not null);
        Assert.Contains(services!, service => service.DisplayName == "stopped-service" && service.State == ServiceRuntimeState.Stopped && service.AllowedActions.SequenceEqual([ServiceActionKind.Start]));
        Assert.Contains(services!, service => service.DisplayName == "missing-service" && service.State == ServiceRuntimeState.NotFound && service.AllowedActions.Count == 0);
    }

    [Fact]
    public async Task Action_IsAntiforgeryProtected_Idempotent_AndAuditedAfterConfirmation()
    {
        await ConfigureAsync("controllable-service");
        factory.ServiceManager.SetStatus("controllable-service", ServiceStatus.Stopped);
        var client = await AdminAsync();
        var service = (await (await client.GetAsync("/api/v1/services")).Content.ReadFromJsonAsync<List<ServiceSummaryDto>>(TestJsonOptions.Default))!.Single();

        var withoutToken = factory.CreateAdminClient();
        var rejected = await withoutToken.PostAsJsonAsync($"/api/v1/services/{service.ServiceId}/actions", new ServiceActionRequestDto(ServiceActionKind.Start, "missing-token"));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var request = new ServiceActionRequestDto(ServiceActionKind.Start, "service-idempotency-key");
        var accepted = await client.PostAsJsonAsync($"/api/v1/services/{service.ServiceId}/actions", request);
        var duplicate = await client.PostAsJsonAsync($"/api/v1/services/{service.ServiceId}/actions", request);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);

        var confirmed = await WaitForAsync(client, service.ServiceId, summary => summary.State == ServiceRuntimeState.Running && summary.LastOutcome == "Start confirmed");
        Assert.Contains(factory.ServiceManager.Controls, control => control.Service == "controllable-service" && control.Action == ServiceControlAction.Start);
        Assert.Equal(ServiceActionKind.Stop, confirmed.AllowedActions.First());

        var auditPath = Path.Combine(factory.FakeConfigRootPath, "audit", "operations.jsonl");
        Assert.Contains("\"category\":\"service\"", await File.ReadAllTextAsync(auditPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedActions_ReportSafeOutcomeWithoutLosingTheObservedState()
    {
        await ConfigureAsync("restricted-service");
        factory.ServiceManager.SetStatus("restricted-service", ServiceStatus.Stopped);
        factory.ServiceManager.Fail("restricted-service", ServiceControlAction.Start, new UnauthorizedAccessException());
        var client = await AdminAsync();
        var service = (await (await client.GetAsync("/api/v1/services")).Content.ReadFromJsonAsync<List<ServiceSummaryDto>>(TestJsonOptions.Default))!.Single();

        var response = await client.PostAsJsonAsync($"/api/v1/services/{service.ServiceId}/actions", new ServiceActionRequestDto(ServiceActionKind.Start, "access-denied"));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var failed = await WaitForAsync(client, service.ServiceId, summary => summary.LastOutcome == "Access denied");
        Assert.Equal(ServiceRuntimeState.Stopped, failed.State);
    }

    private async Task ConfigureAsync(params string[] services)
    {
        var store = factory.Services.GetRequiredService<IAgentConfigurationStore>();
        var configuration = new AgentConfiguration { Services = [.. services], Version = 1 };
        await store.SaveAsync(configuration);
    }

    private async Task<HttpClient> AdminAsync()
    {
        var client = factory.CreateAdminClient();
        var token = await client.GetFromJsonAsync<AntiforgeryTokenDto>("/api/v1/antiforgery");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.Token);
        return client;
    }

    private static async Task<ServiceSummaryDto> WaitForAsync(HttpClient client, string serviceId, Func<ServiceSummaryDto, bool> condition)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var services = await client.GetFromJsonAsync<List<ServiceSummaryDto>>("/api/v1/services", TestJsonOptions.Default);
            var current = services!.Single(service => service.ServiceId == serviceId);
            if (condition(current)) return current;
            await Task.Delay(25);
        }

        throw new Xunit.Sdk.XunitException("Service action did not reach its expected state.");
    }

    public void Dispose()
    {
        foreach (var fileName in new[] { "configuration.json", "secrets.dat" })
        {
            var path = Path.Combine(factory.FakeConfigRootPath, fileName);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
