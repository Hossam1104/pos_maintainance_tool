using System.Text.Json;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Agent.Operations;
using PosAdminTool.Agent.Services;

namespace PosAdminTool.Agent.Endpoints;

public static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder api) => api.MapGet("/events", async (HttpContext context, OperationRegistry registry, ServiceMonitor services) =>
    {
        context.Response.ContentType = "text/event-stream"; context.Response.Headers.CacheControl = "no-cache";
        var completion = new TaskCompletionSource<(string Event, string Payload)>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnOperationChanged(Contracts.V1.Operations.OperationDetailDto detail) => completion.TrySetResult(("operation", JsonSerializer.Serialize(detail)));
        void OnServicesChanged(IReadOnlyList<Contracts.V1.Services.ServiceSummaryDto> detail) => completion.TrySetResult(("services", JsonSerializer.Serialize(detail)));
        registry.Changed += OnOperationChanged; services.Changed += OnServicesChanged;
        try
        {
            await context.Response.WriteAsync("event: connected\ndata: {\"message\":\"Connected.\"}\n\n", context.RequestAborted); await context.Response.Body.FlushAsync(context.RequestAborted);
            var message = await completion.Task.WaitAsync(context.RequestAborted);
            await context.Response.WriteAsync($"event: {message.Event}\ndata: {message.Payload}\n\n", context.RequestAborted); await context.Response.Body.FlushAsync(context.RequestAborted);
        }
        catch (OperationCanceledException) { }
        finally { registry.Changed -= OnOperationChanged; services.Changed -= OnServicesChanged; }
    }).RequireAuthorization(PolicyNames.LocalAdministratorsOnly).WithName("GetEvents");
}
