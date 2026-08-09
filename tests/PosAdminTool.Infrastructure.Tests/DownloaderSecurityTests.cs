using System.Net;
using System.Net.Http;
using PosAdminTool.Infrastructure.Http;
using PosAdminTool.Infrastructure.Smb;

namespace PosAdminTool.Infrastructure.Tests;

public sealed class DownloaderSecurityTests
{
    [Theory]
    [InlineData("ftp://198.51.100.10/trigger")]
    [InlineData("https://user:password@198.51.100.10/trigger")]
    [InlineData("https://127.0.0.1/trigger")]
    [InlineData("https://169.254.169.254/trigger")]
    [InlineData("https://[::ffff:127.0.0.1]/trigger")]
    [InlineData("https://2130706433/trigger")]
    public async Task UnsafeTriggerEndpointForms_FailClosedBeforeSending(string endpoint)
    {
        var handler = new CountingHandler();
        var client = new BackupApiClient(new HttpClient(handler), new FixedResolver(IPAddress.Parse("198.51.100.10")));

        await Assert.ThrowsAsync<BackupApiPolicyException>(() => client.TriggerBackupAsync(endpoint, ["B01"]));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task PrivateConfiguredLiteral_IsAllowedOnlyAsTheExactServerOwnedTarget()
    {
        var handler = new CountingHandler(HttpStatusCode.OK);
        var client = new BackupApiClient(new HttpClient(handler), new FixedResolver(IPAddress.Parse("10.0.0.10")));

        await client.TriggerBackupAsync("https://10.0.0.10/trigger", ["B01"]);

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task HostnameResolvingToPrivateSpace_IsRejectedToPreventDnsSsrfBypass()
    {
        var handler = new CountingHandler();
        var client = new BackupApiClient(new HttpClient(handler), new FixedResolver(IPAddress.Parse("10.0.0.10")));

        await Assert.ThrowsAsync<BackupApiPolicyException>(() => client.TriggerBackupAsync("https://backup.example/trigger", ["B01"]));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task UnsafeRedirect_IsRejectedAndNeverFollowed()
    {
        var handler = new CountingHandler(HttpStatusCode.Redirect, new Uri("https://127.0.0.1/metadata"));
        var client = new BackupApiClient(new HttpClient(handler), new FixedResolver(IPAddress.Parse("198.51.100.10")));

        await Assert.ThrowsAsync<BackupApiPolicyException>(() => client.TriggerBackupAsync("https://198.51.100.10/trigger", ["B01"]));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public void SmbPathPolicy_UsesCanonicalRootAndRejectsTraversalOrUnsafeServers()
    {
        var unc = SmbPathResolver.ToUncPath(
            "192.0.2.10",
            @"D:\DbBackups\batch\B01_1.zip",
            @"D:\DbBackups");

        Assert.Equal(@"\\192.0.2.10\D$\DbBackups\batch\B01_1.zip", unc);
        Assert.Throws<SmbPathPolicyException>(() => SmbPathResolver.ValidateCanonicalRoot(@"\\server\share"));
        Assert.Throws<SmbPathPolicyException>(() => SmbPathResolver.ValidateDrivePath(@"D:\DbBackups\..\other"));
        Assert.Throws<SmbPathPolicyException>(() => SmbPathResolver.ValidateRemoteFilePath(@"D:\Other\B01_1.zip", @"D:\DbBackups"));
        Assert.Throws<SmbPathPolicyException>(() => SmbPathResolver.ValidateServerAddress("127.0.0.1"));
        Assert.Throws<SmbPathPolicyException>(() => SmbPathResolver.ValidateServerAddress("169.254.169.254"));
        Assert.Throws<SmbPathPolicyException>(() => SmbPathResolver.ValidateServerAddress("rdb-server"));
    }

    [Fact]
    public void PreExistingSmbConnection_IsNeverCancelledByThisScope()
    {
        var api = new FakeSmbApi { AddResult = 85 };
        using var scope = SmbConnectionScope.Connect(@"\\192.0.2.10\D$", "svc", "secret", api);

        Assert.Equal(SmbConnectionOutcome.CompatiblePreExisting, scope.Outcome);
        Assert.False(scope.OwnsConnection);
        Assert.Empty(api.CancelledRoots);
    }

    [Fact]
    public void MissingSmbCredentials_UsesTheServiceIdentityWithoutCreatingOwnership()
    {
        var api = new FakeSmbApi { AddResult = 1219 };
        using var scope = SmbConnectionScope.Connect(@"\\[2001:db8::10]\D$", string.Empty, string.Empty, api);

        Assert.Equal(SmbConnectionOutcome.NoCredentialRequired, scope.Outcome);
        Assert.False(scope.OwnsConnection);
        Assert.Empty(api.CancelledRoots);
    }

    [Fact]
    public void OwnedSmbConnection_IsCancelledExactlyOnceOnDispose()
    {
        var api = new FakeSmbApi { AddResult = 0 };
        var scope = SmbConnectionScope.Connect(@"\\192.0.2.10\D$", "svc", "secret", api);
        scope.Dispose();
        scope.Dispose();

        Assert.Equal(SmbConnectionOutcome.EstablishedByScope, scope.Outcome);
        Assert.Single(api.CancelledRoots);
        Assert.False(api.CancelledRoots[0].Force);
    }

    [Fact]
    public void CredentialConflict_IsStableAndDoesNotExposeTheShareOrCredential()
    {
        var api = new FakeSmbApi { AddResult = 1219 };

        var exception = Assert.Throws<SmbConnectionException>(() =>
            SmbConnectionScope.Connect(@"\\192.0.2.10\D$", "svc", "secret", api));

        Assert.Equal("downloader.smb_credential_conflict", exception.Code);
        Assert.DoesNotContain("192.0.2.10", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
    }

    private sealed class CountingHandler(HttpStatusCode statusCode = HttpStatusCode.OK, Uri? location = null) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var response = new HttpResponseMessage(statusCode) { RequestMessage = request };
            if (location is not null) response.Headers.Location = location;
            return Task.FromResult(response);
        }
    }

    private sealed class FixedResolver(params IPAddress[] addresses) : IHostAddressResolver
    {
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken = default) =>
            Task.FromResult((IReadOnlyList<IPAddress>)addresses);
    }

    private sealed class FakeSmbApi : ISmbConnectionApi
    {
        public int AddResult { get; init; }

        public List<(string Root, bool Force)> CancelledRoots { get; } = [];

        public int Add(string shareRoot, string username, string password) => AddResult;

        public int Cancel(string shareRoot, bool force)
        {
            CancelledRoots.Add((shareRoot, force));
            return 0;
        }
    }
}
