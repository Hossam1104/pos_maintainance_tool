namespace PosAdminTool.Agent.Authorization;

public static class PolicyNames
{
    /// <summary>v1 authorizes exactly one principal: a member of the local Administrators group (plan section 5.6). No role matrix.</summary>
    public const string LocalAdministratorsOnly = "LocalAdministratorsOnly";
}
