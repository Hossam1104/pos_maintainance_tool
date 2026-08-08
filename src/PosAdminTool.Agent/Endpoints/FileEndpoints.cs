using PosAdminTool.Agent.Antiforgery;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Agent.Files;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Files;

namespace PosAdminTool.Agent.Endpoints;

public static class FileEndpoints
{
    public static void MapFileEndpoints(this IEndpointRouteBuilder api)
    {
        var files = api.MapGroup("/files").RequireAuthorization(PolicyNames.LocalAdministratorsOnly);

        // Read-only despite the POST verb: it takes a root ID + relative sub-path body, which GET
        // cannot carry cleanly. Never accepts or returns an absolute path (plan section 5.7).
        files.MapPost("/browse", (FileBrowseRequestDto request, IFileBrowseService browseService) =>
        {
            try
            {
                return Results.Ok(browseService.Browse(request.RootId, request.RelativeSubPath));
            }
            catch (FileBrowseValidationException ex)
            {
                return ToProblem(ex);
            }
        })
        .WithName("BrowseFiles")
        .Produces<FileBrowseResultDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // A mutation — it creates server-side handle state — so antiforgery applies.
        files.MapPost("/handles", (FileHandleRequestDto request, HttpContext httpContext, IFileBrowseService browseService, IFileHandleStore handleStore) =>
        {
            try
            {
                var target = browseService.ResolveForHandle(request.RootId, request.RelativeSubPath);
                var principalName = httpContext.User.Identity?.Name ?? string.Empty;
                var handle = handleStore.Issue(principalName, target.RootId, target.RelativeSubPath, request.Purpose);
                return Results.Ok(handle);
            }
            catch (FileHandleStoreCapacityException)
            {
                return Results.Problem(
                    title: "File handle capacity reached",
                    statusCode: StatusCodes.Status429TooManyRequests,
                    extensions: new Dictionary<string, object?>
                    {
                        [ProblemDetailsExtensionKeys.ErrorCode] = ErrorCodes.HandleCapacity,
                    });
            }
            catch (FileBrowseValidationException ex)
            {
                return ToProblem(ex);
            }
        })
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithName("CreateFileHandle")
        .Produces<FileHandleDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static IResult ToProblem(FileBrowseValidationException ex) =>
        Results.Problem(
            title: "File browse request rejected",
            statusCode: ex.StatusCode,
            extensions: new Dictionary<string, object?>
            {
                [ProblemDetailsExtensionKeys.ErrorCode] = ex.ErrorCode,
            });
}
