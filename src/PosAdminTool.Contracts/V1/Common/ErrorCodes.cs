namespace PosAdminTool.Contracts.V1.Common;

/// <summary>
/// Stable, machine-readable error codes carried in <c>ProblemDetails.Extensions["errorCode"]</c>.
/// These are a public contract: renaming one is a breaking change for the Angular client.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string UnknownBrowseRoot = "file_browse.unknown_root";
    public const string PathEscapesRoot = "file_browse.path_escapes_root";
    public const string PathTraversalRejected = "file_browse.path_traversal_rejected";
    public const string AbsolutePathRejected = "file_browse.absolute_path_rejected";
    public const string ReparsePointRejected = "file_browse.reparse_point_rejected";
    public const string UnresolvedEnvironmentVariable = "file_browse.unresolved_environment_variable";
    public const string EntryNotFound = "file_browse.entry_not_found";
    public const string HandleNotFound = "file_handle.not_found";
    public const string HandleExpired = "file_handle.expired";
    public const string HandleAlreadyUsed = "file_handle.already_used";
    public const string HandleWrongPrincipal = "file_handle.wrong_principal";
    public const string HandleWrongPurpose = "file_handle.wrong_purpose";
    public const string ConfigurationVersionConflict = "configuration.version_conflict";
    public const string OperationNotFound = "operation.not_found";
    public const string OperationQueueFull = "operation.queue_full";
    public const string OperationInvalidStateTransition = "operation.invalid_state_transition";
    public const string OperationUnsupported = "operation.unsupported";
    public const string BackupValidationFailed = "backup.validation_failed";
    public const string BackupDestinationInvalid = "backup.destination_invalid";
    public const string BackupDestinationHandleInvalid = "backup.destination_handle_invalid";
    public const string BackupNoComponents = "backup.no_components";
    public const string BackupUnknownComponent = "backup.unknown_component";
    public const string BackupBranchInvalid = "backup.branch_invalid";
    public const string BackupDatabaseInvalid = "backup.database_invalid";
    public const string BackupConfigurationSourceMissing = "backup.configuration_source_missing";
    public const string BackupInsufficientSpace = "backup.insufficient_space";
    public const string BackupArtifactNotFound = "backup.artifact_not_found";
}
