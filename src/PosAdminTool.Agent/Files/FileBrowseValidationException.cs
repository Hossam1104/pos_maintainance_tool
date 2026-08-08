namespace PosAdminTool.Agent.Files;

/// <summary>Carries one of <see cref="Contracts.V1.Common.ErrorCodes"/>'s file-browse codes plus the HTTP status to answer with.</summary>
public sealed class FileBrowseValidationException(string errorCode, int statusCode) : Exception(errorCode)
{
    public string ErrorCode { get; } = errorCode;

    public int StatusCode { get; } = statusCode;
}
