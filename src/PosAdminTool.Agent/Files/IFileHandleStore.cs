using PosAdminTool.Contracts.V1.Files;

namespace PosAdminTool.Agent.Files;

public interface IFileHandleStore
{
    /// <summary>Issues a short-lived, single-use, single-purpose handle bound to the issuing principal (plan section 5.7).</summary>
    FileHandleDto Issue(string principalName, string rootId, string relativeSubPath, FileHandlePurpose purpose);

    /// <summary>Redeems a handle, re-validating principal, purpose, expiry, and single-use at the moment of use.</summary>
    FileHandleRedemption Redeem(string handleId, string principalName, FileHandlePurpose expectedPurpose);
}

public sealed record FileHandleRedemption(bool Success, string? RootId, string? RelativeSubPath, string? FailureErrorCode);
