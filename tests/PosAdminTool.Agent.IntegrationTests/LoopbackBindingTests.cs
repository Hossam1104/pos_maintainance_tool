using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using PosAdminTool.Agent;

namespace PosAdminTool.Agent.IntegrationTests;

/// <summary>
/// Standing regression check (plan section 6.2): the agent must never bind to anything but
/// loopback. Runs a real Kestrel instance on an OS-assigned ephemeral port (never the fixed
/// production port) so the assertion is against actual runtime behavior, not just the source.
/// </summary>
public class LoopbackBindingTests
{
    [Fact]
    public async Task ConfigureLoopbackOnly_NeverBindsToANonLoopbackAddress()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => LoopbackBinding.ConfigureLoopbackOnly(options, port: 0));

        await using var app = builder.Build();
        await app.StartAsync();

        var addressesFeature = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();

        Assert.NotNull(addressesFeature);
        Assert.NotEmpty(addressesFeature.Addresses);
        Assert.All(addressesFeature.Addresses, address =>
        {
            var host = new Uri(address).Host;
            Assert.True(IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip), $"Non-loopback address bound: {address}");
        });

        await app.StopAsync();
    }
}
