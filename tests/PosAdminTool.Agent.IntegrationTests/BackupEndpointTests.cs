using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PosAdminTool.Agent.IntegrationTests.TestSupport;
using PosAdminTool.Contracts.V1.Backups;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Files;
using PosAdminTool.Contracts.V1.Operations;
using PosAdminTool.Contracts.V1.Session;
using PosAdminTool.Domain.Interfaces;

namespace PosAdminTool.Agent.IntegrationTests;

public sealed class BackupEndpointTests : IClassFixture<AgentWebApplicationFactory>, IDisposable
{
    private readonly AgentWebApplicationFactory _factory;

    public BackupEndpointTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateBackup_RunsOperation_RehydratesAndDownloadsOpaqueArtifact()
    {
        await PrepareConfigurationAsync();
        var client = await CreateAdminClientWithAntiforgeryAsync();
        var destinationHandle = await IssueDestinationHandleAsync(client);
        var request = new CreateBackupRequestDto(
            ["branch-database", "branch-config"],
            destinationHandle,
            "backup-integration-idempotency");

        var response = await client.PostAsJsonAsync("/api/v1/backups", request);
        var accepted = await response.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(accepted);

        var duplicate = await client.PostAsJsonAsync("/api/v1/backups", request);
        var duplicateDetail = await duplicate.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(accepted!.OperationId, duplicateDetail!.OperationId);

        var completed = await WaitForCompletionAsync(client, accepted.OperationId);
        var detailJson = await client.GetStringAsync($"/api/v1/operations/{accepted.OperationId}");
        Assert.Equal(OperationState.Succeeded, completed.State);
        Assert.Single(completed.ResultArtifactIds);
        Assert.Equal("test-root / backups", completed.ResolvedDestinationReference);
        Assert.DoesNotContain(_factory.FakeBrowseRootPath, detailJson, StringComparison.OrdinalIgnoreCase);

        var artifactId = completed.ResultArtifactIds[0];
        var metadataResponse = await client.GetAsync($"/api/v1/artifacts/{artifactId}");
        var metadata = await metadataResponse.Content.ReadFromJsonAsync<PosAdminTool.Contracts.V1.Artifacts.ArtifactMetadataDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        Assert.NotNull(metadata);
        Assert.DoesNotContain(_factory.FakeBrowseRootPath, await metadataResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var download = await client.GetAsync($"/api/v1/artifacts/{artifactId}/content");
        var archiveBytes = await download.Content.ReadAsByteArrayAsync();
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Contains("attachment", download.Content.Headers.ContentDisposition?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(metadata!.SizeBytes, archiveBytes.Length);

        using var archive = new ZipArchive(new MemoryStream(archiveBytes), ZipArchiveMode.Read);
        var manifestEntry = archive.GetEntry("manifest.json");
        Assert.NotNull(manifestEntry);
        using var manifestStream = manifestEntry!.Open();
        using var manifest = JsonDocument.Parse(manifestStream);
        Assert.Equal("B001", manifest.RootElement.GetProperty("branchCode").GetString());
        Assert.Contains(archive.Entries, entry => entry.FullName == "RMS_BranchService_appsettings.json");
    }

    [Fact]
    public async Task CreateBackup_WithMissingConfigurationSource_ReturnsSafePreflightProblem()
    {
        await PrepareConfigurationAsync(missingBranchConfig: true);
        var client = await CreateAdminClientWithAntiforgeryAsync();
        var destinationHandle = await IssueDestinationHandleAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/backups",
            new CreateBackupRequestDto(["branch-config"], destinationHandle, "backup-preflight-idempotency"));
        var raw = await response.Content.ReadAsStringAsync();
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ErrorCodes.BackupConfigurationSourceMissing, problem![ProblemDetailsExtensionKeys.ErrorCode].ToString());
        Assert.DoesNotContain(_factory.FakeBrowseRootPath, raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateBackup_WithRestoreHandlePurpose_IsRejected()
    {
        await PrepareConfigurationAsync();
        var client = await CreateAdminClientWithAntiforgeryAsync();
        var restoreHandle = await IssueHandleAsync(client, "backups", FileHandlePurpose.RestoreSource);

        var response = await client.PostAsJsonAsync(
            "/api/v1/backups",
            new CreateBackupRequestDto(["branch-database"], restoreHandle, "backup-purpose-idempotency"));
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.HandleWrongPurpose, problem![ProblemDetailsExtensionKeys.ErrorCode].ToString());
    }

    [Fact]
    public async Task ArtifactEndpoints_RequireAdministratorAndDoNotRevealUnknownStorage()
    {
        var anonymous = await _factory.CreateClient().GetAsync("/api/v1/artifacts/not-an-artifact");
        var nonAdministrator = await _factory.CreateNonAdminClient().GetAsync("/api/v1/artifacts/not-an-artifact");
        var administrator = await _factory.CreateAdminClient().GetAsync("/api/v1/artifacts/not-an-artifact");
        var raw = await administrator.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, nonAdministrator.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, administrator.StatusCode);
        Assert.DoesNotContain("Path", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_factory.FakeBrowseRootPath, raw, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(_factory.FakeBrowseRootPath))
        {
            var attributes = File.GetAttributes(entry);
            if (attributes.HasFlag(FileAttributes.Directory)) Directory.Delete(entry, recursive: true);
            else File.Delete(entry);
        }

        var store = _factory.Services.GetRequiredService<IAgentConfigurationStore>();
        store.SaveAsync(new PosAdminTool.Domain.Models.AgentConfiguration()).GetAwaiter().GetResult();
    }

    private async Task PrepareConfigurationAsync(bool missingBranchConfig = false)
    {
        var branchConfig = Path.Combine(_factory.FakeBrowseRootPath, "branch-appsettings.json");
        var cashierServerConfig = Path.Combine(_factory.FakeBrowseRootPath, "cashier-server-appsettings.json");
        var cashierUiConfig = Path.Combine(_factory.FakeBrowseRootPath, "cashier-ui-appsettings.json");
        var destination = Path.Combine(_factory.FakeBrowseRootPath, "backups");
        Directory.CreateDirectory(destination);
        if (!missingBranchConfig) File.WriteAllText(branchConfig, "{\"branch\":\"B001\"}");
        File.WriteAllText(cashierServerConfig, "{\"server\":true}");
        File.WriteAllText(cashierUiConfig, "{\"ui\":true}");

        var store = _factory.Services.GetRequiredService<IAgentConfigurationStore>();
        var configuration = await store.LoadAsync();
        configuration.BranchCode = "B001";
        configuration.PosNumber = "07";
        configuration.Release = "test-release";
        configuration.ClientName = "Integration Client";
        configuration.Databases = ["RmsCashierSrv", "RmsBranchSrv"];
        configuration.BranchConfigPath = missingBranchConfig ? Path.Combine(_factory.FakeBrowseRootPath, "missing.json") : branchConfig;
        configuration.CashierGrpcConfigPath = cashierServerConfig;
        configuration.CashierUiConfigPath = cashierUiConfig;
        await store.SaveAsync(configuration);
    }

    private async Task<string> IssueDestinationHandleAsync(HttpClient client) =>
        await IssueHandleAsync(client, "backups", FileHandlePurpose.BackupDestination);

    private async Task<string> IssueHandleAsync(HttpClient client, string relativeSubPath, FileHandlePurpose purpose)
    {
        var browseResponse = await client.PostAsJsonAsync(
            "/api/v1/files/browse",
            new FileBrowseRequestDto(AgentWebApplicationFactory.DefaultBrowseRootId, string.Empty));
        var browse = await browseResponse.Content.ReadFromJsonAsync<FileBrowseResultDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.OK, browseResponse.StatusCode);
        Assert.Contains(browse!.Entries, entry => entry.RelativeSubPath == relativeSubPath && entry.IsDirectory);

        var handleResponse = await client.PostAsJsonAsync(
            "/api/v1/files/handles",
            new FileHandleRequestDto(AgentWebApplicationFactory.DefaultBrowseRootId, relativeSubPath, purpose));
        var handle = await handleResponse.Content.ReadFromJsonAsync<FileHandleDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.OK, handleResponse.StatusCode);
        return handle!.HandleId;
    }

    private static async Task<OperationDetailDto> WaitForCompletionAsync(HttpClient client, string operationId)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var response = await client.GetAsync($"/api/v1/operations/{operationId}");
            var detail = await response.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            if (detail!.State is not (OperationState.Queued or OperationState.Running)) return detail;
            await Task.Delay(25);
        }

        throw new TimeoutException("Backup operation did not complete within the integration-test window.");
    }

    private async Task<HttpClient> CreateAdminClientWithAntiforgeryAsync()
    {
        var client = _factory.CreateAdminClient();
        var tokenResponse = await client.GetAsync("/api/v1/antiforgery");
        var payload = await tokenResponse.Content.ReadFromJsonAsync<AntiforgeryTokenDto>(TestJsonOptions.Default);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", payload!.Token);
        return client;
    }
}
