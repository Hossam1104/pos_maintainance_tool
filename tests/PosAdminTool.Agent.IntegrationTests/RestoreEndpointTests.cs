using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PosAdminTool.Agent.IntegrationTests.TestSupport;
using PosAdminTool.Agent.Restore;
using RestoreFailureCodes = PosAdminTool.Application.Restore.RestoreFailureCodes;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Files;
using PosAdminTool.Contracts.V1.Operations;
using PosAdminTool.Contracts.V1.Restore;
using PosAdminTool.Contracts.V1.Session;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Agent.IntegrationTests;

public sealed class RestoreEndpointTests : IClassFixture<AgentWebApplicationFactory>, IDisposable
{
    private readonly AgentWebApplicationFactory _factory;

    public RestoreEndpointTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullRestore_UsesServerPreviewAndFakeExecution_WithSafeEvidence()
    {
        await PrepareConfigurationAsync();
        var archivePath = CreateArchive(
            "B001_restore.zip",
            new Dictionary<string, byte[]>
            {
                ["B001_branch.bak"] = Encoding.UTF8.GetBytes("fake-bak"),
                ["RMS_BranchService_appsettings.json"] = Encoding.UTF8.GetBytes("{\"branch\":\"restored\"}"),
            },
            includeManifest: true);

        var client = await CreateAdminClientWithAntiforgeryAsync();
        var sourceHandle = await IssueHandleAsync(client, "B001_restore.zip");
        var previewResponse = await client.PostAsJsonAsync(
            "/api/v1/restores/preview",
            new RestorePreviewRequestDto(new RestoreSourceDto(null, sourceHandle), RestoreMode.Full));
        var preview = await previewResponse.Content.ReadFromJsonAsync<RestorePreviewDto>(TestJsonOptions.Default);

        Assert.True(previewResponse.StatusCode == HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
        Assert.NotNull(preview);
        Assert.True(preview!.Ready, await previewResponse.Content.ReadAsStringAsync());
        Assert.Equal(2, preview.SqlMovePlan.Count);
        Assert.Equal("RmsBranchSrv.mdf", preview.SqlMovePlan[0].DestinationFileName);
        Assert.Equal("branch-config", preview.ConfigDestinations.Single());
        Assert.DoesNotContain(_factory.FakeBrowseRootPath, await previewResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var executeResponse = await client.PostAsJsonAsync(
            $"/api/v1/restores/{preview.PreviewId}/execute",
            new RestoreExecuteRequestDto(preview.PreviewId, preview.ConfirmationText, "restore-full-integration"));
        var accepted = await executeResponse.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);

        Assert.Equal(HttpStatusCode.Accepted, executeResponse.StatusCode);
        Assert.NotNull(accepted);
        var completed = await WaitForCompletionAsync(client, accepted!.OperationId);
        var operationJson = await client.GetStringAsync($"/api/v1/operations/{accepted.OperationId}");
        Assert.Equal(OperationState.Succeeded, completed.State);
        Assert.DoesNotContain(_factory.FakeBrowseRootPath, operationJson, StringComparison.OrdinalIgnoreCase);
        var auditPath = Path.Combine(_factory.FakeConfigRootPath, "audit", "operations.jsonl");
        Assert.True(File.Exists(auditPath));
        var audit = await File.ReadAllTextAsync(auditPath);
        Assert.DoesNotContain(_factory.FakeBrowseRootPath, audit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"operationMode\":\"full\"", audit, StringComparison.Ordinal);
        Assert.Contains("\"operationTarget\":\"RmsBranchSrv\"", audit, StringComparison.Ordinal);

        var database = (FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>();
        Assert.Single(database.RestoreCalls);
        Assert.Equal("RmsBranchSrv", database.RestoreCalls[0].DatabaseName);
        Assert.Equal("{\"branch\":\"restored\"}", await File.ReadAllTextAsync(Path.Combine(_factory.FakeBrowseRootPath, "branch-appsettings.json")));
        Assert.Contains(_factory.ServiceManager.Controls, item => item is { Service: "TestService", Action: ServiceControlAction.Stop });
        Assert.Contains(_factory.ServiceManager.Controls, item => item is { Service: "TestService", Action: ServiceControlAction.Start });

        // The source is a one-use browse handle. A second preview cannot silently reuse it.
        var replay = await client.PostAsJsonAsync(
            "/api/v1/restores/preview",
            new RestorePreviewRequestDto(new RestoreSourceDto(null, sourceHandle), RestoreMode.Full));
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task RestoreUpload_IsStreamedAndRejectedArchiveLeavesNoStagedUpload()
    {
        await PrepareConfigurationAsync();
        var invalidArchive = CreateArchive(
            "invalid.zip",
            new Dictionary<string, byte[]>
            {
                ["B001_restore.bak"] = Encoding.UTF8.GetBytes("fake-bak"),
                ["unknown.json"] = Encoding.UTF8.GetBytes("{}"),
            },
            includeManifest: false);
        var bytes = await File.ReadAllBytesAsync(invalidArchive);
        var client = await CreateAdminClientWithAntiforgeryAsync();
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/restores/uploads")
        {
            Content = content,
        };
        uploadRequest.Headers.Add("X-Restore-File-Name", "invalid.zip");
        var uploadResponse = await client.SendAsync(uploadRequest);
        var upload = await uploadResponse.Content.ReadFromJsonAsync<RestoreUploadDto>(TestJsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        Assert.NotNull(upload);
        Assert.DoesNotContain(_factory.FakeBrowseRootPath, await uploadResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var previewResponse = await client.PostAsJsonAsync(
            "/api/v1/restores/preview",
            new RestorePreviewRequestDto(new RestoreSourceDto(upload!.UploadId, null), RestoreMode.Full));
        var preview = await previewResponse.Content.ReadFromJsonAsync<RestorePreviewDto>(TestJsonOptions.Default);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, previewResponse.StatusCode);
        Assert.NotNull(preview);
        Assert.False(preview!.Ready);
        Assert.Equal(ErrorCodes.RestoreArchiveUnknownJson, preview.RejectionCode);
        Assert.Equal(0, _factory.Services.GetRequiredService<RestoreUploadStore>().Count);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(Path.GetTempPath(), "PosAdminTool", "restore-uploads"), "*.upload", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task ExecuteRestore_RequiresTypedConfirmationAndOneUseIdempotency()
    {
        await PrepareConfigurationAsync();
        CreateArchive(
            "B001_confirmation.zip",
            new Dictionary<string, byte[]> { ["B001_branch.bak"] = Encoding.UTF8.GetBytes("fake-bak") },
            includeManifest: false);
        var client = await CreateAdminClientWithAntiforgeryAsync();
        var handle = await IssueHandleAsync(client, "B001_confirmation.zip");
        var previewResponse = await client.PostAsJsonAsync(
            "/api/v1/restores/preview",
            new RestorePreviewRequestDto(new RestoreSourceDto(null, handle), RestoreMode.DatabaseOnly));
        var preview = await previewResponse.Content.ReadFromJsonAsync<RestorePreviewDto>(TestJsonOptions.Default);
        Assert.True(preview!.Ready);

        var wrong = await client.PostAsJsonAsync(
            $"/api/v1/restores/{preview.PreviewId}/execute",
            new RestoreExecuteRequestDto(preview.PreviewId, "RESTORE WRONG", "restore-confirmation-idempotency"));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        var wrongProblem = await wrong.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal(ErrorCodes.RestoreConfirmationMismatch, wrongProblem![ProblemDetailsExtensionKeys.ErrorCode].ToString());

        var reused = await client.PostAsJsonAsync(
            $"/api/v1/restores/{preview.PreviewId}/execute",
            new RestoreExecuteRequestDto(preview.PreviewId, preview.ConfirmationText, "restore-confirmation-idempotency"));
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        var reusedProblem = await reused.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal(ErrorCodes.RestoreChallengeUsed, reusedProblem![ProblemDetailsExtensionKeys.ErrorCode].ToString());
    }

    [Fact]
    public async Task ExecuteRestore_RecomputesSourceAndRejectsStaleOrWrongPrincipalIntent()
    {
        await PrepareConfigurationAsync();
        File.Delete(Path.Combine(_factory.FakeBrowseRootPath, "B001_stale.zip"));
        CreateArchive(
            "B001_stale.zip",
            new Dictionary<string, byte[]> { ["B001_branch.bak"] = Encoding.UTF8.GetBytes("original") },
            includeManifest: false);
        var owner = await CreateAdminClientWithAntiforgeryAsync("TESTDOMAIN\\restore-owner");
        var handle = await IssueHandleAsync(owner, "B001_stale.zip");
        var previewResponse = await owner.PostAsJsonAsync(
            "/api/v1/restores/preview",
            new RestorePreviewRequestDto(new RestoreSourceDto(null, handle), RestoreMode.DatabaseOnly));
        var preview = await previewResponse.Content.ReadFromJsonAsync<RestorePreviewDto>(TestJsonOptions.Default);
        Assert.True(preview!.Ready, await previewResponse.Content.ReadAsStringAsync());

        File.Delete(Path.Combine(_factory.FakeBrowseRootPath, "B001_stale.zip"));
        CreateArchive(
            "B001_stale.zip",
            new Dictionary<string, byte[]> { ["B001_branch.bak"] = Encoding.UTF8.GetBytes("source changed") },
            includeManifest: false);
        var otherPrincipal = await CreateAdminClientWithAntiforgeryAsync("TESTDOMAIN\\restore-other");
        var wrongPrincipal = await otherPrincipal.PostAsJsonAsync(
            $"/api/v1/restores/{preview.PreviewId}/execute",
            new RestoreExecuteRequestDto(preview.PreviewId, preview.ConfirmationText, "restore-wrong-principal"));
        Assert.Equal(HttpStatusCode.NotFound, wrongPrincipal.StatusCode);

        var stale = await owner.PostAsJsonAsync(
            $"/api/v1/restores/{preview.PreviewId}/execute",
            new RestoreExecuteRequestDto(preview.PreviewId, preview.ConfirmationText, "restore-stale-source"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var staleProblem = await stale.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal(ErrorCodes.RestoreChallengeChanged, staleProblem![ProblemDetailsExtensionKeys.ErrorCode].ToString());
        Assert.Empty(((FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>()).RestoreCalls);
    }

    [Fact]
    public async Task ConfigOnlyRestore_IsContractedWithoutDatabaseExecution()
    {
        await PrepareConfigurationAsync();
        CreateArchive(
            "B001_config-only.zip",
            new Dictionary<string, byte[]>
            {
                ["branch-config.json"] = Encoding.UTF8.GetBytes("{\"configOnly\":true}"),
            },
            includeManifest: true);
        var client = await CreateAdminClientWithAntiforgeryAsync("TESTDOMAIN\\config-admin");
        var handle = await IssueHandleAsync(client, "B001_config-only.zip");
        var previewResponse = await client.PostAsJsonAsync(
            "/api/v1/restores/preview",
            new RestorePreviewRequestDto(new RestoreSourceDto(null, handle), RestoreMode.ConfigOnly));
        var preview = await previewResponse.Content.ReadFromJsonAsync<RestorePreviewDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.True(preview!.Ready);
        Assert.Empty(preview.SqlMovePlan);

        var execute = await client.PostAsJsonAsync(
            $"/api/v1/restores/{preview.PreviewId}/execute",
            new RestoreExecuteRequestDto(preview.PreviewId, preview.ConfirmationText, "restore-config-only"));
        var accepted = await execute.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.Accepted, execute.StatusCode);
        var duplicate = await client.PostAsJsonAsync(
            $"/api/v1/restores/{preview.PreviewId}/execute",
            new RestoreExecuteRequestDto(preview.PreviewId, preview.ConfirmationText, "restore-config-only"));
        var duplicateDetail = await duplicate.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(accepted!.OperationId, duplicateDetail!.OperationId);
        var completed = await WaitForCompletionAsync(client, accepted!.OperationId);
        Assert.Equal(OperationState.Succeeded, completed.State);
        Assert.Equal("{\"configOnly\":true}", await File.ReadAllTextAsync(Path.Combine(_factory.FakeBrowseRootPath, "branch-appsettings.json")));
        Assert.Empty(((FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>()).RestoreCalls);
    }

    [Fact]
    public async Task RestoreWorker_PreservesFinalizedSuccessWhenCancellationArrivesAtLateBoundary()
    {
        await PrepareConfigurationAsync();
        CreateArchive(
            "B001_worker-late-cancel.zip",
            new Dictionary<string, byte[]> { ["B001_branch.bak"] = Encoding.UTF8.GetBytes("fake-bak") },
            includeManifest: false);

        var client = await CreateAdminClientWithAntiforgeryAsync("TESTDOMAIN\\late-cancel-admin");
        var handle = await IssueHandleAsync(client, "B001_worker-late-cancel.zip");
        var previewResponse = await client.PostAsJsonAsync(
            "/api/v1/restores/preview",
            new RestorePreviewRequestDto(new RestoreSourceDto(null, handle), RestoreMode.DatabaseOnly));
        var preview = await previewResponse.Content.ReadFromJsonAsync<RestorePreviewDto>(TestJsonOptions.Default);
        Assert.True(preview!.Ready, await previewResponse.Content.ReadAsStringAsync());

        var database = (FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>();
        database.BlockVerification = true;
        var execute = await client.PostAsJsonAsync(
            $"/api/v1/restores/{preview.PreviewId}/execute",
            new RestoreExecuteRequestDto(preview.PreviewId, preview.ConfirmationText, "restore-worker-late-cancel"));
        var accepted = await execute.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.Accepted, execute.StatusCode);

        try
        {
            await database.RestoreVerificationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var cancel = await client.PostAsync($"/api/v1/operations/{accepted!.OperationId}/cancel", content: null);
            Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
            database.RestoreVerificationRelease.TrySetResult();

            var completed = await WaitForCompletionAsync(client, accepted.OperationId);
            Assert.Equal(OperationState.Succeeded, completed.State);
            Assert.Null(completed.ErrorCode);
            Assert.True(database.RestoreAttempted);
            Assert.True(database.RestoreCompleted);
        }
        finally
        {
            database.BlockVerification = false;
            database.RestoreVerificationRelease.TrySetResult();
        }
    }

    [Fact]
    public async Task RestoreWorker_ReportsInterruptedSqlAsPartialRecoveryRequired()
    {
        await PrepareConfigurationAsync();
        CreateArchive(
            "B001_worker-interrupted.zip",
            new Dictionary<string, byte[]> { ["B001_branch.bak"] = Encoding.UTF8.GetBytes("fake-bak") },
            includeManifest: false);

        var client = await CreateAdminClientWithAntiforgeryAsync("TESTDOMAIN\\interrupted-admin");
        var handle = await IssueHandleAsync(client, "B001_worker-interrupted.zip");
        var previewResponse = await client.PostAsJsonAsync(
            "/api/v1/restores/preview",
            new RestorePreviewRequestDto(new RestoreSourceDto(null, handle), RestoreMode.DatabaseOnly));
        var preview = await previewResponse.Content.ReadFromJsonAsync<RestorePreviewDto>(TestJsonOptions.Default);
        Assert.True(preview!.Ready, await previewResponse.Content.ReadAsStringAsync());

        var database = (FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>();
        database.RestoreFailure = new InvalidOperationException("secret connection string at C:\\private\\database.bak");
        var execute = await client.PostAsJsonAsync(
            $"/api/v1/restores/{preview.PreviewId}/execute",
            new RestoreExecuteRequestDto(preview.PreviewId, preview.ConfirmationText, "restore-worker-interrupted"));
        var accepted = await execute.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.Accepted, execute.StatusCode);

        var completed = await WaitForCompletionAsync(client, accepted!.OperationId);
        var operationJson = await client.GetStringAsync($"/api/v1/operations/{accepted.OperationId}");
        Assert.Equal(OperationState.PartiallySucceeded, completed.State);
        Assert.Equal(RestoreFailureCodes.DatabaseRestoreInterrupted, completed.ErrorCode);
        Assert.DoesNotContain("secret connection string", operationJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_factory.FakeBrowseRootPath, operationJson, StringComparison.OrdinalIgnoreCase);
        Assert.True(database.RestoreAttempted);
        Assert.False(database.RestoreCompleted);

        var audit = await File.ReadAllTextAsync(Path.Combine(_factory.FakeConfigRootPath, "audit", "operations.jsonl"));
        Assert.Contains("\"state\":\"PartiallySucceeded\"", audit, StringComparison.Ordinal);
        Assert.Contains($"\"errorCode\":\"{RestoreFailureCodes.DatabaseRestoreInterrupted}\"", audit, StringComparison.Ordinal);
        Assert.Contains("\"operationMode\":\"database-only\"", audit, StringComparison.Ordinal);
        Assert.Contains("\"operationTarget\":\"RmsBranchSrv\"", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\private", audit, StringComparison.OrdinalIgnoreCase);
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
        store.SaveAsync(new AgentConfiguration()).GetAwaiter().GetResult();
        ((FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>()).RestoreFileList = [];
        ((FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>()).RestoreCalls.Clear();
        _factory.Services.GetRequiredService<RestoreUploadStore>().Prune();
    }

    private async Task PrepareConfigurationAsync()
    {
        var branchConfig = Path.Combine(_factory.FakeBrowseRootPath, "branch-appsettings.json");
        var cashierServerConfig = Path.Combine(_factory.FakeBrowseRootPath, "cashier-server-appsettings.json");
        var cashierUiConfig = Path.Combine(_factory.FakeBrowseRootPath, "cashier-ui-appsettings.json");
        var dbFiles = Path.Combine(_factory.FakeBrowseRootPath, "db-files");
        Directory.CreateDirectory(dbFiles);
        File.WriteAllText(branchConfig, "{\"branch\":\"original\"}");
        File.WriteAllText(cashierServerConfig, "{\"server\":true}");
        File.WriteAllText(cashierUiConfig, "{\"ui\":true}");

        var store = _factory.Services.GetRequiredService<IAgentConfigurationStore>();
        var configuration = await store.LoadAsync();
        configuration.BranchCode = "B001";
        configuration.Databases = ["RmsBranchSrv"];
        configuration.DbFilesPath = dbFiles;
        configuration.BranchConfigPath = branchConfig;
        configuration.CashierGrpcConfigPath = cashierServerConfig;
        configuration.CashierUiConfigPath = cashierUiConfig;
        configuration.Services = ["TestService"];
        await store.SaveAsync(configuration);

        var database = (FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>();
        database.ResetRestoreState();
        database.RestoreFileList = [new RestoreFileInfo("branch-data", "D"), new RestoreFileInfo("branch-log", "L")];
        database.RestoreFailure = null;
        database.RestoreVerificationResult = true;
        _factory.ServiceManager.SetStatus("TestService", ServiceStatus.Running);
    }

    private string CreateArchive(string fileName, IReadOnlyDictionary<string, byte[]> contents, bool includeManifest, string? manifestBranchCode = null)
    {
        var path = Path.Combine(_factory.FakeBrowseRootPath, fileName);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        var manifestItems = new List<object>();
        foreach (var pair in contents)
        {
            var entry = archive.CreateEntry(pair.Key, CompressionLevel.NoCompression);
            using (var entryStream = entry.Open()) entryStream.Write(pair.Value);
            if (includeManifest)
            {
                var componentId = pair.Key.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)
                    ? "branch-database"
                    : "branch-config";
                manifestItems.Add(new
                {
                    componentId,
                    displayName = pair.Key,
                    archiveName = pair.Key,
                    sizeBytes = pair.Value.LongLength,
                    sha256Checksum = Convert.ToHexString(SHA256.HashData(pair.Value)).ToLowerInvariant(),
                });
            }
        }

        if (includeManifest)
        {
            var manifest = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                branchCode = manifestBranchCode ?? "B001",
                posNumber = "07",
                release = "test",
                contents = manifestItems,
                warnings = Array.Empty<string>(),
            });
            var entry = archive.CreateEntry("manifest.json", CompressionLevel.NoCompression);
            using var entryStream = entry.Open();
            entryStream.Write(manifest);
        }

        return path;
    }

    private async Task<string> IssueHandleAsync(HttpClient client, string relativePath)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/files/handles",
            new FileHandleRequestDto(AgentWebApplicationFactory.DefaultBrowseRootId, relativePath, FileHandlePurpose.RestoreSource));
        var handle = await response.Content.ReadFromJsonAsync<FileHandleDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return handle!.HandleId;
    }

    private async Task<HttpClient> CreateAdminClientWithAntiforgeryAsync(string principalName = "TESTDOMAIN\\admin-user")
    {
        var client = _factory.CreateAdminClient(principalName);
        var tokenResponse = await client.GetAsync("/api/v1/antiforgery");
        var payload = await tokenResponse.Content.ReadFromJsonAsync<AntiforgeryTokenDto>(TestJsonOptions.Default);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", payload!.Token);
        return client;
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

        throw new TimeoutException("Restore operation did not complete within the integration-test window.");
    }
}
