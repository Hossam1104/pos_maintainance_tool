using PosAdminTool.Application.Restore;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Files;
using PosAdminTool.Contracts.V1.Restore;
using PosAdminTool.Agent.Files;

namespace PosAdminTool.Agent.Restore;

/// <summary>Resolves only server-owned upload records and opaque browse handles.</summary>
public sealed class RestoreSourceResolver(
    IFileHandleStore handleStore,
    IFileBrowseService browseService,
    RestoreUploadStore uploads)
{
    public RestoreSourceResolution ResolvePreviewSource(RestoreSourceDto source, string principal)
    {
        if (source is null || (string.IsNullOrWhiteSpace(source.UploadId) == string.IsNullOrWhiteSpace(source.BrowseHandle)))
            throw new RestoreSourceResolutionException(ErrorCodes.RestoreSourceInvalid, StatusCodes.Status400BadRequest);

        if (!string.IsNullOrWhiteSpace(source.UploadId))
        {
            var claimed = uploads.Claim(source.UploadId!, principal);
            if (!claimed.Success || claimed.Descriptor is null)
                throw new RestoreSourceResolutionException(claimed.ErrorCode ?? ErrorCodes.RestoreUploadNotFound, StatusCodes.Status400BadRequest);

            return new RestoreSourceResolution(
                new RestoreSourceReference(RestoreSourceKind.Upload, null, null, claimed.Descriptor.UploadId, claimed.Descriptor.FileName),
                claimed.Descriptor.Path);
        }

        var redemption = handleStore.Redeem(source.BrowseHandle!, principal, FileHandlePurpose.RestoreSource);
        if (!redemption.Success || redemption.RootId is null || redemption.RelativeSubPath is null)
            throw new RestoreSourceResolutionException(redemption.FailureErrorCode ?? ErrorCodes.RestoreSourceInvalid, StatusCodes.Status400BadRequest);

        ResolvedBrowseTarget target;
        try
        {
            target = browseService.ResolveForHandle(redemption.RootId, redemption.RelativeSubPath);
        }
        catch (FileBrowseValidationException ex)
        {
            throw new RestoreSourceResolutionException(ex.ErrorCode, ex.StatusCode);
        }

        return new RestoreSourceResolution(
            new RestoreSourceReference(
                RestoreSourceKind.BrowseHandle,
                target.RootId,
                target.RelativeSubPath,
                null,
                Path.GetFileName(target.CanonicalFullPath)),
            target.CanonicalFullPath);
    }

    public RestoreSourceResolution ResolveStoredSource(RestoreSourceReference reference, string principal)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.Kind == RestoreSourceKind.Upload)
        {
            if (string.IsNullOrWhiteSpace(reference.UploadId)
                || !uploads.TryGetClaimed(reference.UploadId, principal, out var descriptor)
                || descriptor is null)
            {
                throw new RestoreSourceResolutionException(ErrorCodes.RestoreUploadNotFound, StatusCodes.Status400BadRequest);
            }

            return new RestoreSourceResolution(reference with { DisplayName = descriptor.FileName }, descriptor.Path);
        }

        if (string.IsNullOrWhiteSpace(reference.BrowseRootId) || reference.RelativeSubPath is null)
            throw new RestoreSourceResolutionException(ErrorCodes.RestoreSourceInvalid, StatusCodes.Status400BadRequest);

        try
        {
            var target = browseService.ResolveForHandle(reference.BrowseRootId, reference.RelativeSubPath);
            return new RestoreSourceResolution(reference with { DisplayName = Path.GetFileName(target.CanonicalFullPath) }, target.CanonicalFullPath);
        }
        catch (FileBrowseValidationException ex)
        {
            throw new RestoreSourceResolutionException(ex.ErrorCode, ex.StatusCode);
        }
    }
}

public sealed record RestoreSourceResolution(
    RestoreSourceReference Reference,
    string CanonicalPath)
{
    public RestoreSourceDescriptor ToDescriptor() => new(Reference, CanonicalPath);
}

public sealed class RestoreSourceResolutionException(string errorCode, int statusCode)
    : InvalidOperationException("The restore source was rejected.")
{
    public string ErrorCode { get; } = errorCode;

    public int StatusCode { get; } = statusCode;
}
