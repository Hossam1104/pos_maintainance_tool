using System.Net;
using System.Net.Http.Json;
using PosAdminTool.Contracts.V1.Downloader;
using PosAdminTool.Contracts.V1.Operations;

namespace PosAdminTool.Agent.IntegrationTests;

public sealed class DownloaderOperationContractTests
{
    [Fact]
    public void DownloaderOperation_IsAuditedLockedAndKeepsOnlySafeBranchEvidence()
    {
        var entry = new PosAdminTool.Agent.Operations.OperationRegistry.Entry(
            "downloader",
            "B01,B02",
            "TESTDOMAIN\\admin",
            "correlation");

        Assert.Equal(["downloader"], entry.Locks);
        Assert.True(entry.NeedsAudit);
        Assert.False(entry.IsDestructive);
        Assert.True(entry.TryStart());
        entry.SetDownloaderOutcome(new DownloaderOperationOutcomeDto(
            [
                new("B01", DownloaderBranchState.Completed, 100, "password=secret", "C:\\private\\artifact"),
                new("B02", DownloaderBranchState.Failed, 100, "downloader.download_failed")
            ],
            "12345",
            DownloaderTriggerStateDto.Accepted));

        var outcome = entry.ToDto().DownloaderOutcome;

        Assert.NotNull(outcome);
        Assert.Equal(2, outcome!.Branches.Count);
        Assert.Null(outcome.Branches[0].FailureCode);
        Assert.Null(outcome.Branches[0].ArtifactId);
        Assert.Equal("downloader.download_failed", outcome.Branches[1].FailureCode);
        Assert.DoesNotContain("C:\\private", System.Text.Json.JsonSerializer.Serialize(outcome), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", System.Text.Json.JsonSerializer.Serialize(outcome), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DownloaderOperation_UsesOpaqueArtifactCapabilitiesOnly()
    {
        var entry = new PosAdminTool.Agent.Operations.OperationRegistry.Entry(
            "downloader",
            "B01",
            "TESTDOMAIN\\admin",
            "correlation");

        Assert.True(entry.TryStart());
        entry.SetDownloaderOutcome(new DownloaderOperationOutcomeDto(
            [new("B01", DownloaderBranchState.Completed, 100, null, "0123456789abcdef0123456789abcdef")],
            null,
            DownloaderTriggerStateDto.Accepted));

        var dto = entry.ToDto();

        Assert.Equal("0123456789abcdef0123456789abcdef", dto.DownloaderOutcome!.Branches[0].ArtifactId);
        Assert.DoesNotContain("\\", System.Text.Json.JsonSerializer.Serialize(dto.DownloaderOutcome));
    }

    [Fact]
    public void DownloaderOperation_ExposesUnknownTriggerStateAndSanitizesGuidance()
    {
        var entry = new PosAdminTool.Agent.Operations.OperationRegistry.Entry(
            "downloader",
            "B01",
            "TESTDOMAIN\\admin",
            "correlation");

        Assert.True(entry.TryStart());
        entry.SetDownloaderOutcome(new DownloaderOperationOutcomeDto(
            [new("B01", DownloaderBranchState.Failed, 100, "downloader.trigger_outcome_unknown", null)],
            null,
            DownloaderTriggerStateDto.OutcomeUnknown,
            "Check password=secret at C:\\private\\remote before retrying."));

        var outcome = entry.ToDto().DownloaderOutcome;

        Assert.NotNull(outcome);
        Assert.Equal(DownloaderTriggerStateDto.OutcomeUnknown, outcome!.TriggerState);
        Assert.False(outcome.TriggerAccepted);
        Assert.Contains("before retrying", outcome.OperatorGuidance, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", outcome.OperatorGuidance, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\private", outcome.OperatorGuidance, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DownloaderEndpointTests(AgentWebApplicationFactory factory) : IClassFixture<AgentWebApplicationFactory>
{
    [Fact]
    public async Task InvalidBranch_IsRejectedBeforeConfigurationOrNetworkWork()
    {
        var client = await AdminAsync();
        var response = await client.PostAsJsonAsync(
            "/api/v1/downloads/batches",
            new TriggerBatchRequestDto(["../B01"], "downloader-invalid-branch"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("downloader.branch_invalid", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TriggerDestination_IsServerOwnedAndMissingConfigurationFailsClosed()
    {
        var client = await AdminAsync();
        var response = await client.PostAsJsonAsync(
            "/api/v1/downloads/batches",
            new TriggerBatchRequestDto(["B01"], "downloader-no-server-url"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("downloader.endpoint_rejected", body, StringComparison.Ordinal);
        Assert.DoesNotContain("file://", body, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpClient> AdminAsync()
    {
        var client = factory.CreateAdminClient();
        var token = await client.GetFromJsonAsync<PosAdminTool.Contracts.V1.Session.AntiforgeryTokenDto>("/api/v1/antiforgery");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.Token);
        return client;
    }
}
