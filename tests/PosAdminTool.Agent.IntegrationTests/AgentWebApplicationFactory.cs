using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PosAdminTool.Agent.IntegrationTests;

/// <summary>
/// Boots the Agent in-memory (no real socket is opened; the default WebApplicationFactory test
/// server replaces Kestrel) with a disposable fake wwwroot, so the SPA static-file fallback can be
/// exercised without a real Angular build being present.
/// </summary>
public sealed class AgentWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string IndexMarker = "pos-admin-tool-agent-integration-test-shell";

    public string FakeWebRootPath { get; } =
        Directory.CreateTempSubdirectory("pos-admin-agent-wwwroot-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        File.WriteAllText(
            Path.Combine(FakeWebRootPath, "index.html"),
            $"<!doctype html><html><body>{IndexMarker}</body></html>");

        builder.UseEnvironment("Production");
        builder.UseWebRoot(FakeWebRootPath);
    }

    public static bool ResponseContainsIndexMarker(string body) => body.Contains(IndexMarker);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(FakeWebRootPath))
        {
            Directory.Delete(FakeWebRootPath, recursive: true);
        }
    }
}
