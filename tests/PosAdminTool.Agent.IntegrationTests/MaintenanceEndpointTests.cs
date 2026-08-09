using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PosAdminTool.Agent.IntegrationTests.TestSupport;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Maintenance;
using PosAdminTool.Contracts.V1.Operations;
using PosAdminTool.Contracts.V1.Session;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Agent.IntegrationTests;

public sealed class MaintenanceEndpointTests : IClassFixture<AgentWebApplicationFactory>, IDisposable
{
    private readonly AgentWebApplicationFactory _factory;

    public MaintenanceEndpointTests(AgentWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CleanupPreviewAndWorkerExecutionProduceLogicalOutcomeAndAudit()
    {
        var targets = await PrepareCleanupAsync("NORTH_EU_01", ["cleanup-root\\logs"]);
        var target = targets[0];
        _factory.MaintenanceFileSystem.SetEntry(target);
        var client = await CreateAdminClientWithAntiforgeryAsync();

        var previewResponse = await client.PostAsJsonAsync("/api/v1/maintenance/cleanup/preview", new { });
        var preview = await previewResponse.Content.ReadFromJsonAsync<CleanupPreviewDto>(TestJsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.NotNull(preview);
        Assert.True(preview!.Ready);
        Assert.Equal(["cleanup-001"], preview.PathsToDelete);
        Assert.DoesNotContain(target, await previewResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var executeResponse = await client.PostAsJsonAsync(
            "/api/v1/maintenance/cleanup/execute",
            new CleanupExecuteRequestDto(preview.ChallengeId, preview.ConfirmationPhrase) { IdempotencyKey = "maintenance-success-1" });
        var accepted = await executeResponse.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.Accepted, executeResponse.StatusCode);
        Assert.NotNull(accepted);

        var completed = await WaitForCompletionAsync(client, accepted!.OperationId);
        var operationJson = await client.GetStringAsync($"/api/v1/operations/{accepted.OperationId}");
        Assert.Equal(OperationState.Succeeded, completed.State);
        Assert.NotNull(completed.MaintenanceOutcome);
        Assert.Contains(completed.MaintenanceOutcome!.Items, item => item.TargetId == "cleanup-001" && item.Completed);
        Assert.DoesNotContain(target, operationJson, StringComparison.OrdinalIgnoreCase);

        var audit = await File.ReadAllTextAsync(Path.Combine(_factory.FakeConfigRootPath, "audit", "operations.jsonl"));
        Assert.Contains("\"operationType\":\"cleanup\"", audit, StringComparison.Ordinal);
        Assert.Contains("\"state\":\"Succeeded\"", audit, StringComparison.Ordinal);
        Assert.DoesNotContain(target, audit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection string", audit, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CleanupWorkerPreservesPartialResidueAfterAFileFailure()
    {
        var targets = await PrepareCleanupAsync("NORTH_EU_02", ["cleanup-root\\first", "cleanup-root\\second"]);
        var first = targets[0];
        var second = targets[1];
        _factory.MaintenanceFileSystem.SetEntry(first);
        _factory.MaintenanceFileSystem.SetEntry(second);
        _factory.MaintenanceFileSystem.FailDelete(first, new IOException("secret=C:\\private\\path"));
        var client = await CreateAdminClientWithAntiforgeryAsync("TESTDOMAIN\\partial-admin");

        var preview = await ReadCleanupPreviewAsync(client);
        var execute = await client.PostAsJsonAsync(
            "/api/v1/maintenance/cleanup/execute",
            new CleanupExecuteRequestDto(preview.ChallengeId, preview.ConfirmationPhrase) { IdempotencyKey = "maintenance-partial-1" });
        var accepted = await execute.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);
        var completed = await WaitForCompletionAsync(client, accepted!.OperationId);
        var operationJson = await client.GetStringAsync($"/api/v1/operations/{accepted.OperationId}");

        Assert.Equal(OperationState.PartiallySucceeded, completed.State);
        Assert.Equal(ErrorCodes.MaintenanceTargetDeleteFailed, completed.ErrorCode);
        Assert.Contains(completed.MaintenanceOutcome!.Items, item => item.TargetId == "cleanup-001" && item.ResidueUncertain);
        Assert.Contains(completed.MaintenanceOutcome.Items, item => item.TargetId == "cleanup-002" && item.Completed);
        Assert.DoesNotContain("secret=C:", operationJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(first, operationJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(second, operationJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ServiceStopFailureDoesNotContinueIntoFileDeletion()
    {
        var targets = await PrepareCleanupAsync("NORTH_EU_04", ["cleanup-root\\service-failure"]);
        var target = targets[0];
        _factory.MaintenanceFileSystem.SetEntry(target);
        _factory.ServiceManager.Fail("TestService", ServiceControlAction.Stop, new InvalidOperationException("secret=C:\\private\\service"));
        var client = await CreateAdminClientWithAntiforgeryAsync("TESTDOMAIN\\service-failure-admin");

        var preview = await ReadCleanupPreviewAsync(client);
        var execute = await client.PostAsJsonAsync(
            "/api/v1/maintenance/cleanup/execute",
            new CleanupExecuteRequestDto(preview.ChallengeId, preview.ConfirmationPhrase) { IdempotencyKey = "maintenance-service-failure-1" });
        var accepted = await execute.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);
        var completed = await WaitForCompletionAsync(client, accepted!.OperationId);
        var operationJson = await client.GetStringAsync($"/api/v1/operations/{accepted.OperationId}");

        Assert.Equal(OperationState.PartiallySucceeded, completed.State);
        Assert.Equal(ErrorCodes.MaintenanceServiceStopFailed, completed.ErrorCode);
        Assert.True(completed.MaintenanceOutcome!.RecoveryRequired);
        Assert.Contains(completed.MaintenanceOutcome.Items, item => item.Kind == "service" && item.ResidueUncertain);
        Assert.Empty(_factory.MaintenanceFileSystem.DeleteCalls);
        Assert.DoesNotContain("secret=C:", operationJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(target, operationJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BranchResetInterruptedAfterSqlAttemptIsPartialAndAudited()
    {
        await PrepareBranchResetAsync("REGION_A_7");
        var database = (FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>();
        database.BlockReset = true;
        var client = await CreateAdminClientWithAntiforgeryAsync("TESTDOMAIN\\sql-interrupted-admin");
        var preview = await ReadBranchResetPreviewAsync(client);

        var execute = await client.PostAsJsonAsync(
            "/api/v1/maintenance/reset/execute",
            new BranchResetExecuteRequestDto(preview.ChallengeId, preview.ConfirmationPhrase) { IdempotencyKey = "maintenance-sql-interrupted-1" });
        var accepted = await execute.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.Accepted, execute.StatusCode);

        try
        {
            await database.ResetInvocationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var cancel = await client.PostAsync($"/api/v1/operations/{accepted!.OperationId}/cancel", content: null);
            Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
            database.ResetRelease.TrySetResult();

            var completed = await WaitForCompletionAsync(client, accepted.OperationId);
            Assert.Equal(OperationState.PartiallySucceeded, completed.State);
            Assert.Equal(ErrorCodes.MaintenanceSqlResetInterrupted, completed.ErrorCode);
            Assert.Contains(completed.MaintenanceOutcome!.Items, item => item.TargetId == "branch-reset-sql" && item.ResidueUncertain);
            Assert.True(database.ResetAttempted);
            Assert.False(database.ResetCompleted);

            var audit = await File.ReadAllTextAsync(Path.Combine(_factory.FakeConfigRootPath, "audit", "operations.jsonl"));
            Assert.Contains("\"operationType\":\"branch-reset\"", audit, StringComparison.Ordinal);
            Assert.Contains($"\"errorCode\":\"{ErrorCodes.MaintenanceSqlResetInterrupted}\"", audit, StringComparison.Ordinal);
            Assert.Contains("\"branchCode\":\"REGION_A_7\"", audit, StringComparison.Ordinal);
            Assert.DoesNotContain("C:\\private", audit, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            database.BlockReset = false;
            database.ResetRelease.TrySetResult();
        }
    }

    [Fact]
    public async Task SqlResetFailureAfterAttemptIsPartialAndSanitized()
    {
        await PrepareBranchResetAsync("REGION_A_8");
        var database = (FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>();
        database.ResetFailure = new InvalidOperationException("secret=C:\\private\\sql");
        var client = await CreateAdminClientWithAntiforgeryAsync("TESTDOMAIN\\sql-failure-admin");
        var preview = await ReadBranchResetPreviewAsync(client);

        var execute = await client.PostAsJsonAsync(
            "/api/v1/maintenance/reset/execute",
            new BranchResetExecuteRequestDto(preview.ChallengeId, preview.ConfirmationPhrase) { IdempotencyKey = "maintenance-sql-failure-1" });
        var accepted = await execute.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);
        var completed = await WaitForCompletionAsync(client, accepted!.OperationId);
        var operationJson = await client.GetStringAsync($"/api/v1/operations/{accepted.OperationId}");

        Assert.Equal(OperationState.PartiallySucceeded, completed.State);
        Assert.Equal(ErrorCodes.MaintenanceSqlResetFailed, completed.ErrorCode);
        Assert.True(database.ResetAttempted);
        Assert.False(database.ResetCompleted);
        Assert.Contains(completed.MaintenanceOutcome!.Items, item => item.TargetId == "branch-reset-sql" && item.ResidueUncertain);
        Assert.DoesNotContain("secret=C:", operationJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BranchResetVerifiesAndResetsTheSameApprovedDatabaseWithLogicalScopeEvidence()
    {
        await PrepareBranchResetAsync("REGION_A_9");
        var database = (FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>();
        var client = await CreateAdminClientWithAntiforgeryAsync("TESTDOMAIN\\sql-scope-admin");

        var preview = await ReadBranchResetPreviewAsync(client);
        Assert.Equal(["RmsBranchSrv"], database.BranchVerificationDatabases);

        var execute = await client.PostAsJsonAsync(
            "/api/v1/maintenance/reset/execute",
            new BranchResetExecuteRequestDto(preview.ChallengeId, preview.ConfirmationPhrase) { IdempotencyKey = "maintenance-sql-scope-1" });
        var accepted = await execute.Content.ReadFromJsonAsync<OperationDetailDto>(TestJsonOptions.Default);
        var completed = await WaitForCompletionAsync(client, accepted!.OperationId);
        var operationJson = await client.GetStringAsync($"/api/v1/operations/{accepted.OperationId}");

        Assert.Equal(OperationState.Succeeded, completed.State);
        Assert.Equal("RmsBranchSrv", database.ResetCalls.Single().DatabaseName);
        Assert.Equal(["Sales", "CashierSessions"], database.ResetCalls.Single().Tables);
        Assert.DoesNotContain("C:\\private", operationJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection string", operationJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnrelatedBranchDatabaseIsRejectedBeforeVerificationOrReset()
    {
        await PrepareBranchResetAsync("REGION_A_10");
        var configuration = _factory.Services.GetRequiredService<IAgentConfigurationStore>();
        var current = await configuration.LoadAsync();
        current.Maintenance.BranchResetDatabase = "UnrelatedDatabase";
        await configuration.SaveAsync(current);
        var database = (FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>();
        var client = await CreateAdminClientWithAntiforgeryAsync("TESTDOMAIN\\sql-out-of-scope-admin");

        var response = await client.PostAsJsonAsync("/api/v1/maintenance/reset/preview", new { });
        var preview = await response.Content.ReadFromJsonAsync<BranchResetPreviewDto>(TestJsonOptions.Default);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.NotNull(preview);
        Assert.False(preview!.Ready);
        Assert.Contains(preview.Rejections, item => item.Code == ErrorCodes.MaintenanceDatabaseOutOfScope);
        Assert.Empty(database.BranchVerificationDatabases);
        Assert.Empty(database.ResetCalls);
        Assert.DoesNotContain("UnrelatedDatabase", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownConfiguredResetTableIsRejectedBeforeVerificationOrReset()
    {
        await PrepareBranchResetAsync("REGION_A_11");
        var configuration = _factory.Services.GetRequiredService<IAgentConfigurationStore>();
        var current = await configuration.LoadAsync();
        current.Maintenance.BranchResetTables = ["Sales", "CustomerBalances"];
        await configuration.SaveAsync(current);
        var database = (FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>();
        var client = await CreateAdminClientWithAntiforgeryAsync("TESTDOMAIN\\sql-table-scope-admin");

        var response = await client.PostAsJsonAsync("/api/v1/maintenance/reset/preview", new { });
        var preview = await response.Content.ReadFromJsonAsync<BranchResetPreviewDto>(TestJsonOptions.Default);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.NotNull(preview);
        Assert.False(preview!.Ready);
        Assert.Contains(preview.Rejections, item => item.TargetId == "tables");
        Assert.Empty(database.BranchVerificationDatabases);
        Assert.Empty(database.ResetCalls);
    }

    [Fact]
    public async Task BranchVerificationFailureAgainstApprovedDatabaseFailsClosedWithoutReset()
    {
        await PrepareBranchResetAsync("REGION_A_12");
        var database = (FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>();
        database.BranchVerificationFailure = new IOException("secret=C:\\private\\sql");
        var client = await CreateAdminClientWithAntiforgeryAsync("TESTDOMAIN\\sql-verification-failure-admin");

        var response = await client.PostAsJsonAsync("/api/v1/maintenance/reset/preview", new { });
        var preview = await response.Content.ReadFromJsonAsync<BranchResetPreviewDto>(TestJsonOptions.Default);
        var responseJson = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.NotNull(preview);
        Assert.False(preview!.Ready);
        Assert.Contains(preview.Rejections, item => item.Code == ErrorCodes.MaintenanceDatabaseScopeUnavailable);
        Assert.Equal(["RmsBranchSrv"], database.BranchVerificationDatabases);
        Assert.Empty(database.ResetCalls);
        Assert.DoesNotContain("private", responseJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", responseJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StaleCleanupTargetFailsClosedBeforeQueueAndWrongPrincipalCannotRedeemChallenge()
    {
        var targets = await PrepareCleanupAsync("NORTH_EU_03", ["cleanup-root\\stale"]);
        var target = targets[0];
        _factory.MaintenanceFileSystem.SetEntry(target);
        var owner = await CreateAdminClientWithAntiforgeryAsync("TESTDOMAIN\\owner");
        var preview = await ReadCleanupPreviewAsync(owner);

        var other = await CreateAdminClientWithAntiforgeryAsync("TESTDOMAIN\\other");
        var wrongPrincipal = await other.PostAsJsonAsync(
            "/api/v1/maintenance/cleanup/execute",
            new CleanupExecuteRequestDto(preview.ChallengeId, preview.ConfirmationPhrase) { IdempotencyKey = "maintenance-wrong-principal" });
        Assert.Equal(HttpStatusCode.NotFound, wrongPrincipal.StatusCode);

        var configuration = _factory.Services.GetRequiredService<IAgentConfigurationStore>();
        var current = await configuration.LoadAsync();
        current.Maintenance.CleanupTargets = [Path.Combine(Path.GetDirectoryName(target)!, "changed")];
        await configuration.SaveAsync(current);

        var stale = await owner.PostAsJsonAsync(
            "/api/v1/maintenance/cleanup/execute",
            new CleanupExecuteRequestDto(preview.ChallengeId, preview.ConfirmationPhrase) { IdempotencyKey = "maintenance-stale-target" });
        var problem = await stale.Content.ReadFromJsonAsync<Dictionary<string, object>>(TestJsonOptions.Default);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(ErrorCodes.MaintenanceChallengeChanged, problem![ProblemDetailsExtensionKeys.ErrorCode].ToString());
        Assert.Empty(_factory.MaintenanceFileSystem.DeleteCalls);
    }

    public void Dispose()
    {
        _factory.MaintenanceFileSystem.Clear();
        _factory.ServiceManager.Clear();
        _factory.Services.GetRequiredService<IAgentConfigurationStore>().SaveAsync(new AgentConfiguration()).GetAwaiter().GetResult();
        var database = (FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>();
        database.ResetRestoreState();
    }

    private async Task<string[]> PrepareCleanupAsync(string branch, IReadOnlyList<string> relativeTargets)
    {
        var root = Path.Combine(Path.GetTempPath(), "pos-agent-maintenance-fake-root");
        var store = _factory.Services.GetRequiredService<IAgentConfigurationStore>();
        var configuration = await store.LoadAsync();
        configuration.BranchCode = branch;
        configuration.Services = ["TestService"];
        configuration.Databases = ["RmsBranchSrv"];
        configuration.Maintenance = new MaintenanceSettings
        {
            ManagedRoots = [root],
            DataRoots = [root],
            ProtectedRoots = [Path.Combine(root, "protected")],
            InstallRoots = [Path.Combine(root, "install")],
            CleanupTargets = relativeTargets.Select(relative => Path.Combine(root, relative)).ToList(),
            ContinueAfterTargetFailure = true,
        };
        await store.SaveAsync(configuration);
        var paths = configuration.Maintenance.CleanupTargets.ToArray();
        return paths;
    }

    private async Task PrepareBranchResetAsync(string branch)
    {
        var root = Path.Combine(Path.GetTempPath(), "pos-agent-maintenance-fake-root");
        var store = _factory.Services.GetRequiredService<IAgentConfigurationStore>();
        var configuration = await store.LoadAsync();
        configuration.BranchCode = branch;
        configuration.Services = ["TestService"];
        configuration.Databases = ["RmsBranchSrv"];
        configuration.Maintenance = new MaintenanceSettings
        {
            ManagedRoots = [root],
            DataRoots = [root],
            BranchResetDatabase = "RmsBranchSrv",
            BranchResetTables = ["Sales", "CashierSessions"],
        };
        await store.SaveAsync(configuration);
        var database = (FakeDatabaseService)_factory.Services.GetRequiredService<IDatabaseService>();
        database.ResetRestoreState();
        database.BranchResetScope = [new("Sales", 12), new("CashierSessions", 3)];
    }

    private static async Task<CleanupPreviewDto> ReadCleanupPreviewAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/maintenance/cleanup/preview", new { });
        var preview = await response.Content.ReadFromJsonAsync<CleanupPreviewDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(preview!.Ready, await response.Content.ReadAsStringAsync());
        return preview;
    }

    private static async Task<BranchResetPreviewDto> ReadBranchResetPreviewAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/maintenance/reset/preview", new { });
        var preview = await response.Content.ReadFromJsonAsync<BranchResetPreviewDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(preview!.Ready, await response.Content.ReadAsStringAsync());
        return preview;
    }

    private async Task<HttpClient> CreateAdminClientWithAntiforgeryAsync(string principalName = "TESTDOMAIN\\maintenance-admin")
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
            await Task.Delay(10);
        }

        throw new TimeoutException("Maintenance operation did not complete within the integration-test window.");
    }
}
