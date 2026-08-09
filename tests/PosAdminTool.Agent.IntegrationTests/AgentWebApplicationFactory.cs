using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Agent.Files;
using PosAdminTool.Agent.IntegrationTests.TestSupport;
using PosAdminTool.Infrastructure.Configuration;
using PosAdminTool.Domain.Interfaces;

namespace PosAdminTool.Agent.IntegrationTests;

/// <summary>
/// Boots the Agent in-memory (no real socket is opened; the default WebApplicationFactory test
/// server replaces Kestrel) with a disposable fake wwwroot and a disposable fake file-browse root,
/// and substitutes <see cref="FakeAuthenticationHandler"/> for the real Negotiate handler, which
/// needs a live Windows SSPI handshake that an in-memory TestServer cannot perform.
/// </summary>
public sealed class AgentWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string DefaultBrowseRootId = "test-root";
    private const string IndexMarker = "pos-admin-tool-agent-integration-test-shell";

    public string FakeWebRootPath { get; } =
        Directory.CreateTempSubdirectory("pos-admin-agent-wwwroot-").FullName;

    public string FakeBrowseRootPath { get; } =
        Directory.CreateTempSubdirectory("pos-admin-agent-browse-root-").FullName;

    // Isolated stand-ins for %ProgramData%\DBS\PosAdminTool and the legacy
    // %USERPROFILE%\.pos_admin_tool\config.json so integration tests never touch the real machine
    // state (AGENTS.md safety rules: never test against real production paths/credentials).
    public string FakeConfigRootPath { get; } =
        Directory.CreateTempSubdirectory("pos-admin-agent-config-root-").FullName;

    public string FakeLegacyConfigPath { get; } =
        Path.Combine(Directory.CreateTempSubdirectory("pos-admin-agent-legacy-config-").FullName, "config.json");

    public SentinelLogSink LogSink { get; } = new();
    public FakeServiceManager ServiceManager { get; } = new();
    public FakeMaintenanceFileSystem MaintenanceFileSystem { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        File.WriteAllText(
            Path.Combine(FakeWebRootPath, "index.html"),
            $"<!doctype html><html><body>{IndexMarker}</body></html>");

        builder.UseEnvironment("Development");
        builder.UseWebRoot(FakeWebRootPath);
        builder.UseSetting("Testing:DisableNegotiate", "true");

        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(FakeAuthenticationHandler.SchemeName)
                .AddScheme<FakeAuthenticationOptions, FakeAuthenticationHandler>(FakeAuthenticationHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = FakeAuthenticationHandler.SchemeName;
                options.DefaultAuthenticateScheme = FakeAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = FakeAuthenticationHandler.SchemeName;
            });

            services.AddSingleton<IAdministratorGroupChecker, ClaimBasedAdministratorGroupChecker>();

            services.Configure<FileBrowseOptions>(o =>
            {
                o.Roots.Clear();
                o.Roots.Add(new FileBrowseRootOptions
                {
                    RootId = DefaultBrowseRootId,
                    DisplayName = "Test root",
                    AbsolutePath = FakeBrowseRootPath,
                });
            });

            services.AddSingleton(new AgentConfigurationStoreOptions { RootDirectory = FakeConfigRootPath });
            services.AddSingleton(new LegacyConfigurationImporterOptions { SourceFilePath = FakeLegacyConfigPath });
            services.RemoveAll<IDatabaseService>();
            services.AddSingleton<IDatabaseService, FakeDatabaseService>();
            services.RemoveAll<IServiceManager>();
            services.AddSingleton<IServiceManager>(ServiceManager);
            services.RemoveAll<IMaintenanceFileSystem>();
            services.AddSingleton<IMaintenanceFileSystem>(MaintenanceFileSystem);
            services.AddSingleton(LogSink);
            services.AddSingleton<ILoggerProvider, SentinelTestLoggerProvider>();
        });
    }

    public static bool ResponseContainsIndexMarker(string body) => body.Contains(IndexMarker);

    public HttpClient CreateAdminClient(string principalName = "TESTDOMAIN\\admin-user")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(FakeAuthenticationHandler.PrincipalNameHeader, principalName);
        client.DefaultRequestHeaders.Add(FakeAuthenticationHandler.IsAdministratorHeader, "true");
        return client;
    }

    public HttpClient CreateNonAdminClient(string principalName = "TESTDOMAIN\\standard-user")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(FakeAuthenticationHandler.PrincipalNameHeader, principalName);
        client.DefaultRequestHeaders.Add(FakeAuthenticationHandler.IsAdministratorHeader, "false");
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            if (Directory.Exists(FakeWebRootPath))
            {
                Directory.Delete(FakeWebRootPath, recursive: true);
            }

            if (Directory.Exists(FakeBrowseRootPath))
            {
                Directory.Delete(FakeBrowseRootPath, recursive: true);
            }

            if (Directory.Exists(FakeConfigRootPath))
            {
                Directory.Delete(FakeConfigRootPath, recursive: true);
            }

            var legacyConfigDirectory = Path.GetDirectoryName(FakeLegacyConfigPath);
            if (legacyConfigDirectory is not null && Directory.Exists(legacyConfigDirectory))
            {
                Directory.Delete(legacyConfigDirectory, recursive: true);
            }
        }
    }
}
