using System.Text;
using PosAdminTool.Agent.Restore;
using PosAdminTool.Agent.IntegrationTests.TestSupport;
using PosAdminTool.Application.Restore;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Agent.IntegrationTests;

public sealed class RestoreChallengeAndUploadStoreTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly string _root = Directory.CreateTempSubdirectory("pos-restore-state-tests-").FullName;

    [Fact]
    public async Task UploadStoreBoundsSizeAndRemovesRejectedOrCancelledResidue()
    {
        var clock = new ManualTimeProvider(Start);
        var policy = new RuntimeRetentionPolicy
        {
            MaxRestoreUploads = 2,
            MaxRestoreUploadBytes = 256 * 1024,
            MaxRestoreStagedBytes = 512 * 1024,
        };
        using var store = CreateUploadStore(clock, policy);

        await Assert.ThrowsAsync<RestoreUploadInvalidException>(() => store.StageAsync(
            "TEST\\admin",
            "..\\host-path.zip",
            new MemoryStream([1, 2, 3])));

        await Assert.ThrowsAsync<RestoreUploadTooLargeException>(() => store.StageAsync(
            "TEST\\admin",
            "too-large.zip",
            new MemoryStream(new byte[300_000])));
        Assert.Equal(0, store.Count);
        Assert.Empty(UploadFiles());

        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.StageAsync(
            "TEST\\admin",
            "cancelled.zip",
            new CancellingStream(cancellation),
            cancellation.Token));
        Assert.Equal(0, store.Count);
        Assert.Empty(UploadFiles());
    }

    [Fact]
    public async Task UploadStoreBindsClaimsToPrincipalAndExpiresFiles()
    {
        var clock = new ManualTimeProvider(Start);
        using var store = CreateUploadStore(clock, new RuntimeRetentionPolicy { RestoreUploadLifetime = TimeSpan.FromMinutes(1) });
        var upload = await store.StageAsync("TEST\\admin", "restore.zip", new MemoryStream(Encoding.UTF8.GetBytes("archive")));

        var wrongPrincipal = store.Claim(upload.UploadId, "TEST\\other");
        Assert.False(wrongPrincipal.Success);
        Assert.Equal(ErrorCodes.RestoreUploadNotFound, wrongPrincipal.ErrorCode);

        var claim = store.Claim(upload.UploadId, "TEST\\admin");
        Assert.True(claim.Success);
        Assert.True(store.TryGetClaimed(upload.UploadId, "TEST\\admin", out var descriptor));
        Assert.NotNull(descriptor);
        Assert.True(File.Exists(descriptor!.Path));
        Assert.False(store.Claim(upload.UploadId, "TEST\\admin").Success);

        store.Release(upload.UploadId);
        Assert.False(File.Exists(descriptor.Path));
        Assert.Equal(0, store.Count);

        var expiring = await store.StageAsync("TEST\\admin", "expiring.zip", new MemoryStream([1, 2, 3]));
        clock.Advance(TimeSpan.FromMinutes(1));
        var expired = store.Claim(expiring.UploadId, "TEST\\admin");
        Assert.Equal(ErrorCodes.RestoreUploadExpired, expired.ErrorCode);
        Assert.Equal(0, store.Count);
        Assert.Empty(UploadFiles());
    }

    [Fact]
    public async Task UploadStoreReservesConcurrentInFlightBytes()
    {
        var clock = new ManualTimeProvider(Start);
        var policy = new RuntimeRetentionPolicy
        {
            MaxRestoreUploads = 2,
            MaxRestoreUploadBytes = 4,
            MaxRestoreStagedBytes = 4,
        };
        using var store = CreateUploadStore(clock, policy);
        using var blocked = new BlockingReadStream([1, 2, 3, 4]);

        var firstUpload = store.StageAsync("TEST\\admin", "first.zip", blocked);
        await blocked.FirstRead;

        await Assert.ThrowsAsync<RestoreUploadCapacityException>(() => store.StageAsync(
            "TEST\\admin",
            "second.zip",
            new MemoryStream([5])));

        blocked.Release();
        var uploaded = await firstUpload;
        Assert.Equal(4, uploaded.SizeBytes);
        Assert.Equal(1, store.Count);
        Assert.Equal(4, store.StagedBytes);
        Assert.Single(UploadFiles());
    }

    [Fact]
    public void ChallengeBindsPrincipalFingerprintConfirmationAndOneUse()
    {
        var clock = new ManualTimeProvider(Start);
        var policy = new RuntimeRetentionPolicy { MaxRestoreChallenges = 4, RestoreChallengeLifetime = TimeSpan.FromMinutes(2) };
        var store = new RestoreChallengeStore(clock, policy);
        var intent = CreateIntent("fingerprint-a");
        var challenge = store.Issue("TEST\\admin", intent);

        Assert.False(store.Redeem(challenge.ChallengeId, "TEST\\other", intent.Fingerprint, intent.ConfirmationText).Success);
        var success = store.Redeem(challenge.ChallengeId, "TEST\\admin", intent.Fingerprint, intent.ConfirmationText);
        Assert.True(success.Success);
        Assert.Same(intent, success.Intent);
        Assert.Equal(ErrorCodes.RestoreChallengeUsed, store.Redeem(challenge.ChallengeId, "TEST\\admin", intent.Fingerprint, intent.ConfirmationText).ErrorCode);

        var changed = store.Issue("TEST\\admin", CreateIntent("fingerprint-b"));
        Assert.Equal(
            ErrorCodes.RestoreChallengeChanged,
            store.Redeem(changed.ChallengeId, "TEST\\admin", "different", "RESTORE RmsBranchSrv").ErrorCode);

        var confirmation = store.Issue("TEST\\admin", intent);
        Assert.Equal(
            ErrorCodes.RestoreConfirmationMismatch,
            store.Redeem(confirmation.ChallengeId, "TEST\\admin", intent.Fingerprint, "RESTORE WRONG").ErrorCode);

        var expired = store.Issue("TEST\\admin", intent);
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(
            ErrorCodes.RestoreChallengeExpired,
            store.Redeem(expired.ChallengeId, "TEST\\admin", intent.Fingerprint, intent.ConfirmationText).ErrorCode);
    }

    [Fact]
    public void ChallengeRetentionIsBounded()
    {
        var store = new RestoreChallengeStore(
            new ManualTimeProvider(Start),
            new RuntimeRetentionPolicy { MaxRestoreChallenges = 1 });
        store.Issue("TEST\\admin", CreateIntent("one"));
        Assert.Throws<RestoreChallengeCapacityException>(() => store.Issue("TEST\\admin", CreateIntent("two")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private RestoreUploadStore CreateUploadStore(TimeProvider clock, RuntimeRetentionPolicy policy) =>
        new(new RestoreFileSystem(), clock, policy, Path.Combine(_root, "uploads"));

    private IEnumerable<string> UploadFiles()
    {
        var directory = Path.Combine(_root, "uploads");
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.upload", SearchOption.TopDirectoryOnly).ToArray()
            : [];
    }

    private static RestorePreviewIntent CreateIntent(string fingerprint) =>
        new(
            new RestoreSourceReference(RestoreSourceKind.Upload, null, null, "upload-id", "restore.zip"),
            new RestoreSourceIdentity("C:\\server-owned-source.zip", 1, Start, "checksum"),
            RestoreMode.Full,
            "B001",
            "RmsBranchSrv",
            "manifest-v1",
            [new RestoreFileInfo("branch_data", "D")],
            new RestoreSqlPlan("RmsBranchSrv", "C:\\db", []),
            [],
            [],
            1,
            "RESTORE RmsBranchSrv",
            fingerprint);

    private sealed class CancellingStream : MemoryStream
    {
        private readonly CancellationTokenSource _cancellation;
        private bool _cancelled;

        public CancellingStream(CancellationTokenSource cancellation)
            : base(Encoding.UTF8.GetBytes(new string('x', 200_000)))
        {
            _cancellation = cancellation;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_cancelled)
            {
                _cancelled = true;
                var read = base.Read(buffer.Span);
                _cancellation.Cancel();
                return ValueTask.FromResult(read);
            }

            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class BlockingReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        private readonly TaskCompletionSource _firstRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readCount;

        public Task FirstRead => _firstRead.Task;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _readCount) == 1)
            {
                var read = Read(buffer.Span);
                _firstRead.TrySetResult();
                return ValueTask.FromResult(read);
            }

            return WaitForReleaseAsync(cancellationToken);
        }

        public void Release() => _release.TrySetResult();

        private async ValueTask<int> WaitForReleaseAsync(CancellationToken cancellationToken)
        {
            await _release.Task.WaitAsync(cancellationToken);
            return 0;
        }
    }

}
