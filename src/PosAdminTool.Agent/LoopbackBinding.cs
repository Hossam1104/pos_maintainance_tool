using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace PosAdminTool.Agent;

/// <summary>
/// The agent's authoritative bind policy: loopback only. There is no configuration path (command
/// line, environment variable, or appsettings) that can change this — see plan section 5.1 and
/// docs/adr/003-loopback-only.md. This is the only place Kestrel endpoints are configured, so a
/// test can assert the policy directly instead of trusting it by inspection.
/// </summary>
public static class LoopbackBinding
{
    public const int DefaultPort = 5001;

    public static void ConfigureLoopbackOnly(KestrelServerOptions options, int port = DefaultPort)
    {
        options.Listen(IPAddress.Loopback, port);
    }
}
