using System.Security.Claims;
using PosAdminTool.Agent.Authorization;

namespace PosAdminTool.Agent.IntegrationTests.TestSupport;

/// <summary>Test double for <see cref="IAdministratorGroupChecker"/> — no real WindowsIdentity is available under the in-memory TestServer.</summary>
public sealed class ClaimBasedAdministratorGroupChecker : IAdministratorGroupChecker
{
    public bool IsInAdministratorsGroup(ClaimsPrincipal principal) =>
        principal.HasClaim(FakeAuthenticationHandler.AdministratorClaimType, "true");
}
