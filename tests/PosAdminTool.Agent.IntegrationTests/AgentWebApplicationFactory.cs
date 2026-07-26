using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Agent.Files;
using PosAdminTool.Agent.IntegrationTests.TestSupport;

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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        File.WriteAllText(
            Path.Combine(FakeWebRootPath, "index.html"),
            $"<!doctype html><html><body>{IndexMarker}</body></html>");

        builder.UseEnvironment("Production");
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
        }
    }
}
