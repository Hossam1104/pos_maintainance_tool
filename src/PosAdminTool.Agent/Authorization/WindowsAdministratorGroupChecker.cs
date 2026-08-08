using System.Security.Claims;
using System.Security.Principal;

namespace PosAdminTool.Agent.Authorization;

/// <summary>
/// The real production check. Negotiate authentication on Windows produces a <see cref="ClaimsPrincipal"/>
/// backed by a genuine <see cref="WindowsIdentity"/>; <see cref="WindowsPrincipal.IsInRole(WindowsBuiltInRole)"/>
/// queries the actual OS token rather than relying on claim-shaped role strings, which a bare
/// <c>ClaimsPrincipal.IsInRole</c> call would not reliably match against Windows group SIDs.
/// </summary>
public sealed class WindowsAdministratorGroupChecker : IAdministratorGroupChecker
{
    public bool IsInAdministratorsGroup(ClaimsPrincipal principal)
    {
        return principal.Identity is WindowsIdentity windowsIdentity
            && new WindowsPrincipal(windowsIdentity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
