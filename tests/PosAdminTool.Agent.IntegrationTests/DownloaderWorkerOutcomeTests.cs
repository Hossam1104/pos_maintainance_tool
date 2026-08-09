using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PosAdminTool.Agent.IntegrationTests.TestSupport;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Downloader;
using PosAdminTool.Contracts.V1.Operations;
using PosAdminTool.Contracts.V1.Session;
using PosAdminTool.Domain.Exceptions;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Agent.IntegrationTests;

[CollectionDefinition("Downloader worker integration", DisableParallelization = true)]
public sealed class DownloaderWorkerIntegrationCollection : ICollectionFixture<AgentWebApplicationFactory>;

[Collection("Downloader worker integration")]
public sealed class DownloaderWorkerOutcomeTests
{
    private const string ApiUrl = "http://198.51.100.10/trigger";
    private const string RdbServerIp = "192.0.2.10";
    private const string BackupRoot = @"D:\DbBackups";

    private readonly AgentWebApplicationFactory _factory;

    public DownloaderWorkerOutcomeTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Worker_TriggerRejectedBeforeAcceptance_IsFailedAndAuditedWithFalseMilestone()
    {
        await PrepareAsync(["B01"]);
        _factory.DownloaderApiClient.TriggerFailure =
            new DownloaderTriggerException(DownloaderFailureCodes.EndpointRejected);

        using var client = await CreateAdminClientAsync("TESTDOMAIN\\downloader-trigger-rejected");
        var accepted = await SubmitAsync(client, ["B01"], "downloader-worker-trigger-rejected");
        var completed = await WaitForCompletionAsync(client, accepted.OperationId);
        var operationJson = await client.GetStringAsync($"/api/v1/operations/{accepted.OperationId}");
        var audit = await ReadAuditAsync();

        Assert.Equal(OperationState.Failed, completed.State);
        Assert.Equal(DownloaderFailureCodes.EndpointRejected, completed.ErrorCode);
        Assert.False(completed.DownloaderOutcome!.TriggerAccepted);
        Assert.Equal(DownloaderFailureCodes.EndpointRejected, completed.DownloaderOutcome.Branches.Single().FailureCode);
        Assert.Equal(0, _factory.DownloaderRepository.ListDirectoriesCalls);
        Assert.Contains($"\"errorCode\":\"{DownloaderFailureCodes.EndpointRejected}\"", audit, StringComparison.Ordinal);
        Assert.Contains("\"TriggerAccepted\":false", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("The backup trigger could not be completed", operationJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Worker_TriggerAcceptedThenSmbFailure_PreservesAcceptedMilestoneAndRepositoryCode()
    {
        await PrepareAsync(["B01", "B02"]);
        _factory.DownloaderRepository.ListDirectoriesFailure =
            new BackupRepositoryException(DownloaderFailureCodes.SmbConnectionFailed);

        using var client = await CreateAdminClientAsync("TESTDOMAIN\\downloader-smb-failure");
        var accepted = await SubmitAsync(client, ["B01", "B02"], "downloader-worker-smb-failure");
        var completed = await WaitForCompletionAsync(client, accepted.OperationId);
        var operationJson = await client.GetStringAsync($"/api/v1/operations/{accepted.OperationId}");
        var audit = await ReadAuditAsync();

        Assert.Equal(OperationState.Failed, completed.State);
        Assert.Equal(DownloaderFailureCodes.SmbConnectionFailed, completed.ErrorCode);
        Assert.True(completed.DownloaderOutcome!.TriggerAccepted);
        Assert.All(completed.DownloaderOutcome.Branches, branch =>
            Assert.Equal(DownloaderFailureCodes.SmbConnectionFailed, branch.FailureCode));
        Assert.Contains($"\"errorCode\":\"{DownloaderFailureCodes.SmbConnectionFailed}\"", operationJson, StringComparison.Ordinal);
        Assert.Contains("\"TriggerAccepted\":true", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("downloader.trigger_failed", operationJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Worker_AcceptedTriggerWithIndependentBranchFailure_PreservesArtifactAndPartialTruth()
    {
        await PrepareAsync(["B01", "B02"]);
        ConfigureReadyBatch(["B01", "B02"]);
        _factory.DownloaderRepository.FailedDownloadBranches.Add("B02");

        using var client = await CreateAdminClientAsync("TESTDOMAIN\\downloader-partial");
        var accepted = await SubmitAsync(client, ["B01", "B02"], "downloader-worker-partial");
        var completed = await WaitForCompletionAsync(client, accepted.OperationId);
        var operationJson = await client.GetStringAsync($"/api/v1/operations/{accepted.OperationId}");
        var audit = await ReadAuditAsync();
        var success = completed.DownloaderOutcome!.Branches.Single(branch => branch.BranchCode == "B01");
        var failed = completed.DownloaderOutcome.Branches.Single(branch => branch.BranchCode == "B02");

        Assert.Equal(OperationState.PartiallySucceeded, completed.State);
        Assert.Equal(DownloaderFailureCodes.PartialFailure, completed.ErrorCode);
        Assert.True(completed.DownloaderOutcome.TriggerAccepted);
        Assert.Equal(DownloaderBranchState.Completed, success.State);
        Assert.NotNull(success.ArtifactId);
        Assert.Contains(success.ArtifactId!, completed.ResultArtifactIds);
        Assert.Equal(DownloaderBranchState.Failed, failed.State);
        Assert.Equal(DownloaderFailureCodes.SmbConnectionFailed, failed.FailureCode);
        Assert.Contains("\"triggerAccepted\":true", operationJson, StringComparison.Ordinal);
        Assert.Contains(DownloaderFailureCodes.SmbConnectionFailed, audit, StringComparison.Ordinal);
        Assert.DoesNotContain(BackupRoot, operationJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(BackupRoot, audit, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Worker_AcceptedTriggerThenCancellation_PreservesAcceptedMilestone()
    {
        await PrepareAsync(["B01"]);
        _factory.DownloaderRepository.BlockListDirectories = true;

        using var client = await CreateAdminClientAsync("TESTDOMAIN\\downloader-cancelled");
        var accepted = await SubmitAsync(client, ["B01"], "downloader-worker-cancelled");
        await _factory.DownloaderRepository.ListDirectoriesStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            var cancel = await client.PostAsync($"/api/v1/operations/{accepted.OperationId}/cancel", content: null);
            Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

            var completed = await WaitForCompletionAsync(client, accepted.OperationId);
            Assert.Equal(OperationState.Cancelled, completed.State);
            Assert.True(completed.DownloaderOutcome!.TriggerAccepted);
            Assert.Equal(DownloaderBranchState.Cancelled, completed.DownloaderOutcome.Branches.Single().State);

            var audit = await ReadAuditAsync();
            Assert.Contains("\"state\":\"Cancelled\"", audit, StringComparison.Ordinal);
            Assert.Contains("\"TriggerAccepted\":true", audit, StringComparison.Ordinal);
        }
        finally
        {
            _factory.DownloaderRepository.BlockListDirectories = false;
        }
    }

    private async Task PrepareAsync(IReadOnlyList<string> branchCodes)
    {
        _factory.DownloaderApiClient.Reset();
        _factory.DownloaderRepository.Reset();

        var configuration = await _factory.Services
            .GetRequiredService<IAgentConfigurationStore>()
            .LoadAsync();
        configuration.Downloader = new AgentDownloaderConfiguration
        {
            ApiUrl = ApiUrl,
            RdbServerIp = RdbServerIp,
            BackupRootFolder = BackupRoot,
            KnownBranchCodes = [.. branchCodes],
            PollIntervalSeconds = 1,
            TimeoutSeconds = 30,
            StableSizeObservationAttempts = 2,
            StableSizeObservationIntervalSeconds = 1,
        };
        await _factory.Services
            .GetRequiredService<IAgentConfigurationStore>()
            .SaveAsync(configuration);

        var auditPath = Path.Combine(_factory.FakeConfigRootPath, "audit", "operations.jsonl");
        if (File.Exists(auditPath)) File.Delete(auditPath);
    }

    private void ConfigureReadyBatch(IReadOnlyList<string> branchCodes)
    {
        var now = DateTimeOffset.UtcNow;
        var folder = Path.Combine(BackupRoot, "batch-001");
        _factory.DownloaderRepository.Directories.Add(new("batch-001", folder, now, 0));
        _factory.DownloaderRepository.FilesByFolder[folder] = branchCodes
            .Select(branch => new RemoteEntryInfo(
                $"{branch}_001.zip",
                Path.Combine(folder, $"{branch}_001.zip"),
                now,
                1024))
            .ToList();
    }

    private async Task<HttpClient> CreateAdminClientAsync(string principal)
    {
        var client = _factory.CreateAdminClient(principal);
        var token = await client.GetFromJsonAsync<AntiforgeryTokenDto>("/api/v1/antiforgery");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.Token);
        return client;
    }

    private static async Task<OperationDetailDto> SubmitAsync(
        HttpClient client,
        IReadOnlyList<string> branchCodes,
        string idempotencyKey)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/downloads/batches",
            new TriggerBatchRequestDto(branchCodes, idempotencyKey));
        var detail = await response.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(detail);
        return detail!;
    }

    private static async Task<OperationDetailDto> WaitForCompletionAsync(HttpClient client, string operationId)
    {
        for (var attempt = 0; attempt < 500; attempt++)
        {
            var response = await client.GetAsync($"/api/v1/operations/{operationId}");
            var detail = await response.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            if (detail!.State is not (OperationState.Queued or OperationState.Running)) return detail;
            await Task.Delay(10);
        }

        throw new TimeoutException("Downloader operation did not complete within the integration-test window.");
    }

    private async Task<string> ReadAuditAsync()
    {
        var path = Path.Combine(_factory.FakeConfigRootPath, "audit", "operations.jsonl");
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (File.Exists(path)) return await File.ReadAllTextAsync(path);
            await Task.Delay(10);
        }

        throw new FileNotFoundException("Downloader audit record was not written.", path);
    }
}
