using System.Net;
using System.Net.Http.Json;
using PosAdminTool.Agent.IntegrationTests.TestSupport;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Configuration;
using PosAdminTool.Contracts.V1.Session;

namespace PosAdminTool.Agent.IntegrationTests;

/// <summary>
/// Sentinel-only secret values throughout (AGENTS.md safety rules: never test with real
/// production credentials). Each test resets the isolated config/secret directory in
/// <see cref="Dispose"/> so tests in this class do not observe each other's state, matching
/// <see cref="FileEndpointTests"/>'s convention for its shared browse root.
/// </summary>
public class ConfigurationEndpointTests : IClassFixture<AgentWebApplicationFactory>, IDisposable
{
    private static readonly DownloaderConfigurationUpdateRequestDto EmptyDownloaderUpdate =
        new(string.Empty, string.Empty, string.Empty, null, [], 5, 1800);

    private const long InitialVersion = 1;

    private readonly AgentWebApplicationFactory _factory;

    public ConfigurationEndpointTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_Unauthenticated_IsRejected()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/configuration");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_AuthenticatedNonAdministrator_IsForbidden()
    {
        var client = _factory.CreateNonAdminClient();

        var response = await client.GetAsync("/api/v1/configuration");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_FreshEnvironment_ReturnsDefaultsWithNoCredentialAndNeitherSecretPresent()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/configuration");
        var body = await response.Content.ReadFromJsonAsync<RedactedConfigurationDto>(TestJsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(string.Empty, body!.SqlInstance);
        Assert.Equal(string.Empty, body.ApiBaseUrl);
        Assert.Empty(body.Databases);
        Assert.False(body.HasSqlPassword);
        Assert.False(body.Downloader.HasRdbPassword);
        Assert.Equal(string.Empty, body.Downloader.RdbServerIp);
        Assert.Empty(body.Downloader.KnownBranchCodes);
    }

    [Fact]
    public async Task Put_WithoutAntiforgeryToken_IsRejected()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.PutAsJsonAsync("/api/v1/configuration", NewUpdateRequest(0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_SettingBothSecrets_NeverReturnsTheirValueInTheResponseBody()
    {
        const string sqlSentinel = "sentinel-endpoint-sql-pw";
        const string rdbSentinel = "sentinel-endpoint-rdb-pw";
        var client = await CreateAdminClientWithAntiforgeryAsync();
        var request = NewUpdateRequest(InitialVersion, sqlPassword: sqlSentinel, rdbPassword: rdbSentinel);
        _factory.LogSink.Clear();

        var response = await client.PutAsJsonAsync("/api/v1/configuration", request);
        var raw = await response.Content.ReadAsStringAsync();
        var body = await response.Content.ReadFromJsonAsync<RedactedConfigurationDto>(TestJsonOptions.Default, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(sqlSentinel, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(rdbSentinel, raw, StringComparison.Ordinal);
        Assert.True(body!.HasSqlPassword);
        Assert.True(body.Downloader.HasRdbPassword);

        var configFile = Path.Combine(_factory.FakeConfigRootPath, "configuration.json");
        var persisted = await File.ReadAllTextAsync(configFile);
        Assert.DoesNotContain(sqlSentinel, persisted, StringComparison.Ordinal);
        Assert.DoesNotContain(rdbSentinel, persisted, StringComparison.Ordinal);
        Assert.DoesNotContain(sqlSentinel, _factory.LogSink.Messages, StringComparison.Ordinal);
        Assert.DoesNotContain(rdbSentinel, _factory.LogSink.Messages, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Put_WithBlankSecretFields_KeepsTheExistingSecrets()
    {
        const string sqlSentinel = "sentinel-keep-sql-pw";
        var client = await CreateAdminClientWithAntiforgeryAsync();
        var afterSet = await PutAsync(client, NewUpdateRequest(InitialVersion, sqlPassword: sqlSentinel));

        var afterBlankUpdate = await PutAsync(client, NewUpdateRequest(afterSet.Version, sqlPassword: null));

        Assert.True(afterBlankUpdate.HasSqlPassword);
    }

    [Fact]
    public async Task Put_WithStaleExpectedVersion_ReturnsVersionConflictProblem()
    {
        var client = await CreateAdminClientWithAntiforgeryAsync();
        await PutAsync(client, NewUpdateRequest(InitialVersion, sqlInstance: "FIRST"));

        var response = await client.PutAsJsonAsync("/api/v1/configuration", NewUpdateRequest(InitialVersion, sqlInstance: "STALE"));
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(ErrorCodes.ConfigurationVersionConflict, problem![ProblemDetailsExtensionKeys.ErrorCode].ToString());
    }

    [Fact]
    public async Task ClearSecret_RemovesTheSecretAndNeverReturnsItsValue()
    {
        const string sqlSentinel = "sentinel-clear-sql-pw";
        var client = await CreateAdminClientWithAntiforgeryAsync();
        var afterSet = await PutAsync(client, NewUpdateRequest(InitialVersion, sqlPassword: sqlSentinel));

        var response = await client.PostAsJsonAsync(
            "/api/v1/configuration/secrets/clear",
            new ClearSecretRequestDto(SecretKind.SqlPassword, afterSet.Version));
        var raw = await response.Content.ReadAsStringAsync();
        var body = await response.Content.ReadFromJsonAsync<RedactedConfigurationDto>(TestJsonOptions.Default, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(sqlSentinel, raw, StringComparison.Ordinal);
        Assert.False(body!.HasSqlPassword);
    }

    [Fact]
    public async Task ClearSecret_WithoutAntiforgeryToken_IsRejected()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/configuration/secrets/clear",
            new ClearSecretRequestDto(SecretKind.SqlPassword, 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<RedactedConfigurationDto> PutAsync(HttpClient client, ConfigurationUpdateRequestDto request)
    {
        var response = await client.PutAsJsonAsync("/api/v1/configuration", request);
        var body = await response.Content.ReadFromJsonAsync<RedactedConfigurationDto>(TestJsonOptions.Default);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body!;
    }

    private static ConfigurationUpdateRequestDto NewUpdateRequest(
        long expectedVersion,
        string sqlInstance = "",
        string? sqlPassword = null,
        string? rdbPassword = null) => new(
            sqlInstance,
            string.Empty,
            sqlPassword,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            [],
            EmptyDownloaderUpdate with { RdbPassword = rdbPassword },
            expectedVersion);

    private async Task<HttpClient> CreateAdminClientWithAntiforgeryAsync()
    {
        var client = _factory.CreateAdminClient();
        var tokenResponse = await client.GetAsync("/api/v1/antiforgery");
        var payload = await tokenResponse.Content.ReadFromJsonAsync<AntiforgeryTokenDto>();
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", payload!.Token);
        return client;
    }

    public void Dispose()
    {
        foreach (var fileName in new[] { "configuration.json", "secrets.dat" })
        {
            var path = Path.Combine(_factory.FakeConfigRootPath, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
