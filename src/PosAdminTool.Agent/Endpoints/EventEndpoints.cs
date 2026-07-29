using System.Text.Json;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Agent.Operations;
namespace PosAdminTool.Agent.Endpoints;
public static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder api) => api.MapGet("/events", async (HttpContext context, OperationRegistry registry) =>
    {
        context.Response.ContentType = "text/event-stream"; context.Response.Headers.CacheControl = "no-cache";
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(Contracts.V1.Operations.OperationDetailDto detail) => completion.TrySetResult(JsonSerializer.Serialize(detail));
        registry.Changed += OnChanged;
        try { await context.Response.WriteAsync("event: connected\ndata: {\"message\":\"Connected.\"}\n\n", context.RequestAborted); await context.Response.Body.FlushAsync(context.RequestAborted); var payload = await completion.Task.WaitAsync(context.RequestAborted); await context.Response.WriteAsync($"event: operation\ndata: {payload}\n\n", context.RequestAborted); await context.Response.Body.FlushAsync(context.RequestAborted); }
        catch (OperationCanceledException) { }
        finally { registry.Changed -= OnChanged; }
    }).RequireAuthorization(PolicyNames.LocalAdministratorsOnly).WithName("GetEvents");
}
