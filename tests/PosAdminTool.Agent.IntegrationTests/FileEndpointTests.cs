using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Files;
using PosAdminTool.Contracts.V1.Session;

namespace PosAdminTool.Agent.IntegrationTests;

public class FileEndpointTests : IClassFixture<AgentWebApplicationFactory>, IDisposable
{
    private readonly AgentWebApplicationFactory _factory;
    private readonly List<string> _extraTempDirectories = [];

    public FileEndpointTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Browse_Unauthenticated_IsRejected()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/files/browse", new FileBrowseRequestDto(AgentWebApplicationFactory.DefaultBrowseRootId, string.Empty));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Browse_AuthenticatedNonAdministrator_IsForbidden()
    {
        var client = _factory.CreateNonAdminClient();

        var response = await client.PostAsJsonAsync("/api/v1/files/browse", new FileBrowseRequestDto(AgentWebApplicationFactory.DefaultBrowseRootId, string.Empty));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Browse_RootDirectory_ListsChildrenWithRelativePaths()
    {
        File.WriteAllText(Path.Combine(_factory.FakeBrowseRootPath, "backup.bak"), "fake");
        Directory.CreateDirectory(Path.Combine(_factory.FakeBrowseRootPath, "subdir"));

        var client = _factory.CreateAdminClient();
        var response = await client.PostAsJsonAsync("/api/v1/files/browse", new FileBrowseRequestDto(AgentWebApplicationFactory.DefaultBrowseRootId, string.Empty));
        var body = await response.Content.ReadFromJsonAsync<FileBrowseResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains(body!.Entries, e => e.Name == "backup.bak" && !e.IsDirectory && e.RelativeSubPath == "backup.bak");
        Assert.Contains(body.Entries, e => e.Name == "subdir" && e.IsDirectory && e.RelativeSubPath == "subdir");
    }

    [Fact]
    public async Task Browse_UnknownRootId_IsRejected()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync("/api/v1/files/browse", new FileBrowseRequestDto("no-such-root", string.Empty));
        var problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.UnknownBrowseRoot, problem);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("child/../../escape")]
    [InlineData("../../../Windows")]
    public async Task Browse_ParentTraversal_IsRejected(string maliciousSubPath)
    {
        var client = _factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync("/api/v1/files/browse", new FileBrowseRequestDto(AgentWebApplicationFactory.DefaultBrowseRootId, maliciousSubPath));
        var problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.PathTraversalRejected, problem);
    }

    [Theory]
    [InlineData(@"C:\Windows")]
    [InlineData(@"\\server\share")]
    [InlineData(@"\rooted")]
    public async Task Browse_AbsolutePath_IsRejected(string absoluteSubPath)
    {
        var client = _factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync("/api/v1/files/browse", new FileBrowseRequestDto(AgentWebApplicationFactory.DefaultBrowseRootId, absoluteSubPath));
        var problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.AbsolutePathRejected, problem);
    }

    [Fact]
    public async Task Browse_UnresolvedEnvironmentVariable_IsRejected()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync("/api/v1/files/browse", new FileBrowseRequestDto(AgentWebApplicationFactory.DefaultBrowseRootId, "%TEMP%\\evil"));
        var problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.UnresolvedEnvironmentVariable, problem);
    }

    [Fact]
    public async Task Browse_JunctionPointingOutsideRoot_IsRejected()
    {
        var outsideTarget = Directory.CreateTempSubdirectory("pos-admin-agent-outside-root-").FullName;
        _extraTempDirectories.Add(outsideTarget);
        File.WriteAllText(Path.Combine(outsideTarget, "secret.txt"), "should never be listed");

        var junctionPath = Path.Combine(_factory.FakeBrowseRootPath, "escape-junction");
        CreateDirectoryJunction(junctionPath, outsideTarget);

        var client = _factory.CreateAdminClient();
        var response = await client.PostAsJsonAsync("/api/v1/files/browse", new FileBrowseRequestDto(AgentWebApplicationFactory.DefaultBrowseRootId, "escape-junction"));
        var problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ReparsePointRejected, problem);
    }

    [Fact]
    public async Task Handles_WithoutAntiforgeryToken_IsRejected()
    {
        File.WriteAllText(Path.Combine(_factory.FakeBrowseRootPath, "restore-source.bak"), "fake");
        var client = _factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/files/handles",
            new FileHandleRequestDto(AgentWebApplicationFactory.DefaultBrowseRootId, "restore-source.bak", FileHandlePurpose.RestoreSource));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Handles_WithAntiforgeryToken_IssuesAnOpaqueHandle()
    {
        File.WriteAllText(Path.Combine(_factory.FakeBrowseRootPath, "restore-source.bak"), "fake");
        var client = await CreateAdminClientWithAntiforgeryAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/files/handles",
            new FileHandleRequestDto(AgentWebApplicationFactory.DefaultBrowseRootId, "restore-source.bak", FileHandlePurpose.RestoreSource));
        var handle = await response.Content.ReadFromJsonAsync<FileHandleDto>(TestSupport.TestJsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(handle);
        Assert.False(string.IsNullOrWhiteSpace(handle!.HandleId));
        Assert.Equal(FileHandlePurpose.RestoreSource, handle.Purpose);
        Assert.True(handle.ExpiresAtUtc > DateTimeOffset.UtcNow);
    }

    private async Task<HttpClient> CreateAdminClientWithAntiforgeryAsync()
    {
        var client = _factory.CreateAdminClient();
        var tokenResponse = await client.GetAsync("/api/v1/antiforgery");
        var payload = await tokenResponse.Content.ReadFromJsonAsync<AntiforgeryTokenDto>();
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", payload!.Token);
        return client;
    }

    private static async Task<string?> ReadProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return problem is not null && problem.TryGetValue(ProblemDetailsExtensionKeys.ErrorCode, out var code)
            ? code.ToString()
            : null;
    }

    private static void CreateDirectoryJunction(string junctionPath, string targetPath)
    {
        var startInfo = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junctionPath}\" \"{targetPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)!;
        process.WaitForExit(10_000);

        if (process.ExitCode != 0 || !Directory.Exists(junctionPath))
        {
            var stderr = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Failed to create test junction '{junctionPath}' -> '{targetPath}': {stderr}");
        }
    }

    public void Dispose()
    {
        foreach (var directory in _extraTempDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        // Clear any files/junctions this test class created inside the shared FakeBrowseRootPath so
        // other tests in the same class run against a clean root.
        foreach (var entry in Directory.EnumerateFileSystemEntries(_factory.FakeBrowseRootPath))
        {
            var attributes = File.GetAttributes(entry);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                Directory.Delete(entry);
            }
            else if (attributes.HasFlag(FileAttributes.Directory))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }
        }
    }
}
