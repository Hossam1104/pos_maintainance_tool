using PosAdminTool.Agent;

// A Windows Service is launched by the Service Control Manager with an unpredictable current
// working directory (commonly System32), not the publish folder. Anchor the content/web root to
// the executable's own directory explicitly rather than trusting the process cwd, or static file
// and SPA fallback serving silently break outside a plain `dotnet run` from the project directory.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.WebHost.ConfigureKestrel(options => LoopbackBinding.ConfigureLoopbackOnly(options));

var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));

var webRootPath = app.Environment.WebRootPath;
if (!string.IsNullOrEmpty(webRootPath) && Directory.Exists(webRootPath))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();

    if (app.Environment.IsProduction() && File.Exists(Path.Combine(webRootPath, "index.html")))
    {
        app.MapFallbackToFile("index.html");
    }
}

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
