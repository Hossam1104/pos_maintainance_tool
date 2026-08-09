using PosAdminTool.Agent.IntegrationTests.TestSupport;
using PosAdminTool.Agent.Maintenance;
using PosAdminTool.Application.Maintenance;
using PosAdminTool.Contracts.V1.Common;

namespace PosAdminTool.Agent.IntegrationTests;

public sealed class MaintenanceChallengeStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FreshChallengeIsPrincipalBoundOneUseAndWrongConfirmationFailsClosed()
    {
        var clock = new ManualTimeProvider(Start);
        var store = new MaintenanceChallengeStore(
            clock,
            new RuntimeRetentionPolicy
            {
                MaxMaintenanceChallenges = 4,
                MaintenanceChallengeLifetime = TimeSpan.FromMinutes(5),
            });
        var intent = Intent();
        var challenge = store.Issue("TESTDOMAIN\\owner", intent);

        Assert.False(store.TryGetIntent(challenge.ChallengeId, "TESTDOMAIN\\other", out _, out var wrongPrincipal));
        Assert.Equal(ErrorCodes.MaintenanceChallengeWrongPrincipal, wrongPrincipal);

        var wrong = store.Redeem(challenge.ChallengeId, "TESTDOMAIN\\owner", intent.Fingerprint, "WRONG");
        Assert.False(wrong.Success);
        Assert.Equal(ErrorCodes.MaintenanceConfirmationMismatch, wrong.ErrorCode);

        var reused = store.Redeem(challenge.ChallengeId, "TESTDOMAIN\\owner", intent.Fingerprint, intent.ConfirmationText);
        Assert.False(reused.Success);
        Assert.Equal(ErrorCodes.MaintenanceChallengeUsed, reused.ErrorCode);
    }

    [Fact]
    public void FreshChallengeRedeemsOnceAndExpiredChallengeFailsClosed()
    {
        var clock = new ManualTimeProvider(Start);
        var store = new MaintenanceChallengeStore(clock, new RuntimeRetentionPolicy());
        var intent = Intent();
        var challenge = store.Issue("TESTDOMAIN\\owner", intent);

        var redeemed = store.Redeem(challenge.ChallengeId, "TESTDOMAIN\\owner", intent.Fingerprint, intent.ConfirmationText);
        Assert.True(redeemed.Success);
        Assert.Equal(intent, redeemed.Intent);
        Assert.Equal(ErrorCodes.MaintenanceChallengeUsed, store.Redeem(challenge.ChallengeId, "TESTDOMAIN\\owner", intent.Fingerprint, intent.ConfirmationText).ErrorCode);

        var expiredChallenge = store.Issue("TESTDOMAIN\\owner", intent);
        clock.Advance(TimeSpan.FromMinutes(5));
        var expired = store.Redeem(expiredChallenge.ChallengeId, "TESTDOMAIN\\owner", intent.Fingerprint, intent.ConfirmationText);
        Assert.False(expired.Success);
        Assert.Equal(ErrorCodes.MaintenanceChallengeExpired, expired.ErrorCode);
    }

    private static MaintenancePreviewIntent Intent() => new(
        MaintenanceMode.Cleanup,
        "NORTH_EU_01",
        string.Empty,
        [],
        ["cleanup-001"],
        "CONFIRM CLEANUP ABC123",
        "fingerprint");
}
