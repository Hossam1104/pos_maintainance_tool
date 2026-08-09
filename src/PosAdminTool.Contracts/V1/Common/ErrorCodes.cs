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
    public const string HandleCapacity = "file_handle.capacity";
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
    public const string BackupArtifactCatalogFull = "backup.artifact_catalog_full";

    public const string RestoreSourceInvalid = "restore.source_invalid";
    public const string RestoreUploadInvalid = "restore.upload_invalid";
    public const string RestoreUploadTooLarge = "restore.upload_too_large";
    public const string RestoreUploadCapacity = "restore.upload_capacity";
    public const string RestoreUploadNotFound = "restore.upload_not_found";
    public const string RestoreUploadExpired = "restore.upload_expired";
    public const string RestoreUploadAlreadyClaimed = "restore.upload_already_claimed";
    public const string RestoreArchiveInvalid = "restore.archive_invalid";
    public const string RestoreArchivePathRejected = "restore.archive_path_rejected";
    public const string RestoreArchiveEntryLimit = "restore.archive_entry_limit";
    public const string RestoreArchiveExpandedSizeLimit = "restore.archive_expanded_size_limit";
    public const string RestoreArchiveCompressionRatio = "restore.archive_compression_ratio";
    public const string RestoreArchiveExtensionRejected = "restore.archive_extension_rejected";
    public const string RestoreArchiveDuplicateEntry = "restore.archive_duplicate_entry";
    public const string RestoreArchiveManifestInvalid = "restore.archive_manifest_invalid";
    public const string RestoreArchiveChecksumMismatch = "restore.archive_checksum_mismatch";
    public const string RestoreArchiveBranchMismatch = "restore.archive_branch_mismatch";
    public const string RestoreArchiveBakAmbiguous = "restore.archive_bak_ambiguous";
    public const string RestoreArchiveUnknownJson = "restore.archive_unknown_json";
    public const string RestoreDestinationUnsafe = "restore.destination_unsafe";
    public const string RestoreSqlPlanInvalid = "restore.sql_plan_invalid";
    public const string RestorePreviewNotReady = "restore.preview_not_ready";
    public const string RestoreChallengeNotFound = "restore.challenge_not_found";
    public const string RestoreChallengeExpired = "restore.challenge_expired";
    public const string RestoreChallengeUsed = "restore.challenge_used";
    public const string RestoreChallengeWrongPrincipal = "restore.challenge_wrong_principal";
    public const string RestoreChallengeChanged = "restore.challenge_changed";
    public const string RestoreConfirmationMismatch = "restore.confirmation_mismatch";
    public const string RestoreOperationQueueFull = "restore.operation_queue_full";
    public const string RestoreFailed = "restore.failed";
    public const string RestoreVerificationFailed = "restore.verification_failed";
    public const string RestoreConfigCopyFailed = "restore.config_copy_failed";
    public const string RestoreServiceStopFailed = "restore.service_stop_failed";
    public const string RestoreServiceRestartFailed = "restore.service_restart_failed";
}
