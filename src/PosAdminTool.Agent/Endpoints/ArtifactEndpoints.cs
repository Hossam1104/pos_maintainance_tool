using PosAdminTool.Agent.Artifacts;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Contracts.V1.Artifacts;
using PosAdminTool.Contracts.V1.Common;

namespace PosAdminTool.Agent.Endpoints;

public static class ArtifactEndpoints
{
    public static void MapArtifactEndpoints(this IEndpointRouteBuilder api)
    {
        var artifacts = api.MapGroup("/artifacts").RequireAuthorization(PolicyNames.LocalAdministratorsOnly);

        artifacts.MapGet("/{artifactId}", (string artifactId, HttpContext context, ArtifactCatalog catalog) =>
        {
            var principal = context.User.Identity?.Name ?? string.Empty;
            return catalog.TryGet(principal, artifactId, out ArtifactMetadataDto? metadata)
                ? Results.Ok(metadata)
                : NotFound();
        })
        .WithName("GetArtifactMetadata")
        .Produces<ArtifactMetadataDto>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        artifacts.MapGet("/{artifactId}/content", async (string artifactId, HttpContext context, ArtifactCatalog catalog, CancellationToken cancellationToken) =>
        {
            var principal = context.User.Identity?.Name ?? string.Empty;
            var stream = await catalog.OpenReadAsync(principal, artifactId, cancellationToken).ConfigureAwait(false);
            if (stream is null) return NotFound();

            if (!catalog.TryGet(principal, artifactId, out var metadata) || metadata is null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
                return NotFound();
            }

            return Results.File(stream, "application/zip", metadata.DisplayName, enableRangeProcessing: false);
        })
        .WithName("DownloadArtifact")
        .Produces(StatusCodes.Status200OK, contentType: "application/zip")
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static IResult NotFound() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        extensions: new Dictionary<string, object?> { [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.BackupArtifactNotFound });
}
