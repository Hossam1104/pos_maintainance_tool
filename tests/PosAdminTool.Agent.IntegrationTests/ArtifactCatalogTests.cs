using PosAdminTool.Agent;
using PosAdminTool.Agent.Artifacts;
using PosAdminTool.Agent.IntegrationTests.TestSupport;
using PosAdminTool.Infrastructure.Backups;

namespace PosAdminTool.Agent.IntegrationTests;

public sealed class ArtifactCatalogTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly string _root = Directory.CreateTempSubdirectory("pos-admin-artifact-catalog-").FullName;

    [Fact]
    public async Task Artifact_RemainsDownloadableWithinWindow_AndExpiresFailClosed()
    {
        var clock = new ManualTimeProvider(Start);
        var catalog = CreateCatalog(clock);
        var path = CreateArtifact("valid.zip", "artifact-content");
        var metadata = catalog.Register("DOMAIN\\alice", "backup.zip", path, new FileInfo(path).Length, "checksum", Start);

        Assert.Equal(Start.AddHours(1), metadata.ExpiresAtUtc);
        Assert.True(catalog.TryGet("DOMAIN\\alice", metadata.ArtifactId, out _));
        await using (var stream = await catalog.OpenReadAsync("DOMAIN\\alice", metadata.ArtifactId, CancellationToken.None))
        {
            Assert.NotNull(stream);
            Assert.Equal((byte)'a', stream!.ReadByte());
        }

        clock.Advance(TimeSpan.FromHours(1));

        Assert.False(catalog.TryGet("DOMAIN\\alice", metadata.ArtifactId, out _));
        Assert.False(File.Exists(path));
        Assert.Null(await catalog.OpenReadAsync("DOMAIN\\alice", metadata.ArtifactId, CancellationToken.None));
    }

    [Fact]
    public async Task Expiry_DoesNotDeleteAnActiveDownloadUntilItsLeaseIsDisposed()
    {
        var clock = new ManualTimeProvider(Start);
        var catalog = CreateCatalog(clock);
        var path = CreateArtifact("leased.zip", "artifact-content");
        var metadata = catalog.Register("DOMAIN\\alice", "leased.zip", path, new FileInfo(path).Length, "checksum", Start);
        var stream = await catalog.OpenReadAsync("DOMAIN\\alice", metadata.ArtifactId, CancellationToken.None);

        clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(0, catalog.Prune());
        Assert.True(File.Exists(path));
        Assert.True(catalog.TryGet("DOMAIN\\alice", metadata.ArtifactId, out _));

        await stream!.DisposeAsync();

        Assert.False(File.Exists(path));
        Assert.False(catalog.TryGet("DOMAIN\\alice", metadata.ArtifactId, out _));
    }

    [Fact]
    public async Task MissingArtifact_FailsClosedWithoutRevealingStorage()
    {
        var clock = new ManualTimeProvider(Start);
        var catalog = CreateCatalog(clock);
        var path = CreateArtifact("missing.zip", "artifact-content");
        var metadata = catalog.Register("DOMAIN\\alice", "missing.zip", path, new FileInfo(path).Length, "checksum", Start);
        File.Delete(path);

        Assert.False(catalog.TryGet("DOMAIN\\alice", metadata.ArtifactId, out _));
        Assert.Null(await catalog.OpenReadAsync("DOMAIN\\alice", metadata.ArtifactId, CancellationToken.None));
        Assert.DoesNotContain(_root, metadata.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FullCatalog_RefusesNewArtifactWithoutDeletingAValidDownload()
    {
        var clock = new ManualTimeProvider(Start);
        var policy = new RuntimeRetentionPolicy { MaxArtifacts = 1 };
        var catalog = new ArtifactCatalog(new PhysicalBackupFileSystem(), clock, policy);
        var firstPath = CreateArtifact("first.zip", "first");
        var secondPath = CreateArtifact("second.zip", "second");
        var first = catalog.Register("DOMAIN\\alice", "first.zip", firstPath, 5, "checksum", Start);

        Assert.Throws<ArtifactCatalogCapacityException>(() => catalog.Register("DOMAIN\\alice", "second.zip", secondPath, 6, "checksum", Start));
        Assert.True(catalog.TryGet("DOMAIN\\alice", first.ArtifactId, out _));
        Assert.True(File.Exists(firstPath));
        Assert.False(File.Exists(secondPath));
    }

    [Fact]
    public async Task ArtifactAccess_IsPrincipalScoped()
    {
        var clock = new ManualTimeProvider(Start);
        var catalog = CreateCatalog(clock);
        var path = CreateArtifact("scoped.zip", "artifact-content");
        var metadata = catalog.Register("DOMAIN\\alice", "scoped.zip", path, new FileInfo(path).Length, "checksum", Start);

        Assert.False(catalog.TryGet("DOMAIN\\mallory", metadata.ArtifactId, out _));
        Assert.Null(await catalog.OpenReadAsync("DOMAIN\\mallory", metadata.ArtifactId, CancellationToken.None));
        Assert.True(File.Exists(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private ArtifactCatalog CreateCatalog(ManualTimeProvider clock) =>
        new(new PhysicalBackupFileSystem(), clock, new RuntimeRetentionPolicy
        {
            MaxArtifacts = 4,
            ArtifactLifetime = TimeSpan.FromHours(1),
        });

    private string CreateArtifact(string name, string contents)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, contents);
        return path;
    }
}
