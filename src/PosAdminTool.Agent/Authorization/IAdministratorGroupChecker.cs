using System.Security.Claims;

namespace PosAdminTool.Agent.Authorization;

/// <summary>
/// Abstracts the "is this authenticated principal a member of the local Administrators group"
/// check so the real Windows-token check (which needs a live Negotiate handshake, not reproducible
/// in an in-memory test host) can be substituted with a fake in tests.
/// </summary>
public interface IAdministratorGroupChecker
{
    bool IsInAdministratorsGroup(ClaimsPrincipal principal);
}
