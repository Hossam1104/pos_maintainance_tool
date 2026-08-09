using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using PosAdminTool.Agent;
using PosAdminTool.Agent.Artifacts;
using PosAdminTool.Agent.Authorization;
using PosAdminTool.Agent.Correlation;
using PosAdminTool.Agent.Endpoints;
using PosAdminTool.Agent.Files;
using PosAdminTool.Agent.Operations;
using PosAdminTool.Agent.Restore;
using PosAdminTool.Agent.Maintenance;
using PosAdminTool.Agent.Audit;
using PosAdminTool.Agent.Configuration;
using PosAdminTool.Agent.Device;
using PosAdminTool.Agent.Services;
using PosAdminTool.Application.Services;
using PosAdminTool.Application.UseCases;
using PosAdminTool.Application.Restore;
using PosAdminTool.Application.Maintenance;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Infrastructure.Configuration;
using PosAdminTool.Infrastructure.Backups;
using PosAdminTool.Infrastructure.Http;
using PosAdminTool.Infrastructure.Smb;
using PosAdminTool.Infrastructure.Windows;

// A Windows Service is launched by the Service Control Manager with an unpredictable current
// working directory (commonly System32), not the publish folder. Anchor the content/web root to
// the executable's own directory explicitly rather than trusting the process cwd, or static file
// and SPA fallback serving silently break outside a plain `dotnet run` from the project directory.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.WebHost.ConfigureKestrel(options =>
{
    LoopbackBinding.ConfigureLoopbackOnly(options);
    options.Limits.MaxRequestBodySize = 1 * 1024 * 1024; // 1 MB default for JSON API bodies; upload endpoints override per-route.
});

// Negotiate (Windows Integrated) authentication, authorizing exactly one principal — membership of
// the local Administrators group. There is no role matrix in v1 (plan section 5.6).
//
// The real NegotiateHandler implements IAuthenticationRequestHandler, so the authentication
// middleware invokes it on every request regardless of which scheme is selected as default; it
// immediately throws NotSupportedException ("requires a server that supports IConnectionItemsFeature
// like Kestrel") because the in-memory WebApplicationFactory TestServer doesn't implement that
// feature. This is a real, documented ASP.NET Core limitation, not something fixable in test setup
// alone, so integration tests disable registering it at all and substitute a fake scheme instead.
var authenticationBuilder = builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme);
if (!builder.Configuration.GetValue("Testing:DisableNegotiate", false))
{
    authenticationBuilder.AddNegotiate();
}
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PolicyNames.LocalAdministratorsOnly, policy => policy
        .RequireAuthenticatedUser()
        .Requirements.Add(new LocalAdministratorsOnlyRequirement()));
});
builder.Services.AddSingleton<IAuthorizationHandler, LocalAdministratorsOnlyHandler>();
builder.Services.AddSingleton<IAdministratorGroupChecker, WindowsAdministratorGroupChecker>();

// Antiforgery double-submit cookie: the token cookie itself is intentionally not HttpOnly so the
// Angular shell can read it and mirror it into the request header (plan section 5.1/6.1). It never
// carries session identity or a secret, only a random anti-CSRF value.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.Configure<FileBrowseOptions>(builder.Configuration.GetSection(FileBrowseOptions.SectionName));
builder.Services.AddSingleton<IFileBrowseService, FileBrowseService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(RuntimeRetentionPolicy.Default);
builder.Services.AddSingleton<IFileHandleStore, InMemoryFileHandleStore>();
builder.Services.AddSingleton<IBackupFileSystem, PhysicalBackupFileSystem>();
builder.Services.AddSingleton<IMaintenanceFileSystem, PhysicalMaintenanceFileSystem>();
builder.Services.AddSingleton<IRestoreFileSystem, PhysicalRestoreFileSystem>();
builder.Services.AddSingleton(RestoreArchiveLimits.Default);
builder.Services.AddSingleton<IRestoreSqlPlanBuilder, RestoreSqlPlanBuilder>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddSingleton<IHostAddressResolver, SystemHostAddressResolver>();
builder.Services.AddHttpClient<IBackupApiClient, BackupApiClient>()
    .ConfigurePrimaryHttpMessageHandler(services =>
        BackupApiHttpMessageHandlerFactory.Create(services.GetRequiredService<IHostAddressResolver>()));
builder.Services.AddSingleton<IBackupRepository, SmbBackupRepository>();
builder.Services.AddSingleton<DbDownloadService>();
builder.Services.AddSingleton<RestoreService>();
builder.Services.AddSingleton<ArtifactCatalog>();
builder.Services.AddSingleton<OperationRegistry>();
builder.Services.AddSingleton<ResourceLockSet>();
builder.Services.AddSingleton<OperationAuditWriter>();
builder.Services.AddSingleton<RestoreUploadStore>();
builder.Services.AddSingleton<RestoreChallengeStore>();
builder.Services.AddSingleton<MaintenanceService>();
builder.Services.AddSingleton<MaintenanceChallengeStore>();
builder.Services.AddSingleton<RestoreSourceResolver>();
builder.Services.AddHostedService<OperationWorker>();

// Service-owned, ACL-restricted configuration and secret storage (plan section 5.5). Tests override
// both options singletons to point at an isolated temporary directory instead of the real
// %ProgramData%/%USERPROFILE%.
builder.Services.AddSingleton(new AgentConfigurationStoreOptions());
builder.Services.AddSingleton(new LegacyConfigurationImporterOptions());
builder.Services.AddSingleton<IAgentConfigurationStore, JsonAgentConfigurationStore>();
builder.Services.AddSingleton<IAgentSecretStore, DpapiAgentSecretStore>();
builder.Services.AddSingleton<ILegacyConfigurationImporter, LegacyConfigurationImporter>();
builder.Services.AddSingleton<AgentConfigurationUseCase>();
builder.Services.AddSingleton<IConfigurationService, AgentLegacyConfigurationService>();
builder.Services.AddSingleton<IDatabaseService, SqlCmdExecutor>();
builder.Services.AddSingleton<TestConnectionUseCase>();
builder.Services.AddSingleton<BranchVerificationService>();
builder.Services.AddSingleton<ImportFromRmsUseCase>();
builder.Services.AddSingleton<DeviceDiagnosticsService>();
builder.Services.AddSingleton<IServiceManager, WindowsServiceManager>();
builder.Services.AddSingleton<ServiceCatalog>();
builder.Services.AddSingleton<ServiceMonitor>();
builder.Services.AddSingleton<ServiceCommandCoordinator>();
builder.Services.AddHostedService<ServiceCommandWorker>();
builder.Services.AddHostedService<ServicePollingWorker>();

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var correlationId = CorrelationIdContext.TryGet(context.HttpContext);
        if (correlationId is not null)
        {
            context.ProblemDetails.Extensions[ProblemDetailsExtensionKeys.CorrelationId] = correlationId;
        }
    };
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Enums as camelCase strings, matching property casing, not raw C# member names or numbers.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services.AddOpenApi();

var app = builder.Build();

// One-time, non-secret legacy config.json import (plan section 5.5). Idempotent and read-only
// against the legacy file, so it is safe to run on every startup; a failure here must never prevent
// the Agent from serving requests with whatever configuration already exists.
try
{
    var legacyImporter = app.Services.GetRequiredService<ILegacyConfigurationImporter>();
    await legacyImporter.ImportAsync().ConfigureAwait(false);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Legacy configuration import did not complete; continuing with the existing Agent configuration.");
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Never exposes developer exception pages outside Development (plan section 5.1). The
    // registered ProblemDetails service produces a safe, generic response for unhandled exceptions.
    app.UseExceptionHandler();
}

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers.TryGetValue(CorrelationIdContext.HeaderName, out var existing)
        && !string.IsNullOrWhiteSpace(existing)
        ? existing.ToString()
        : Guid.NewGuid().ToString("N");

    context.Items[CorrelationIdContext.HttpContextItemKey] = correlationId;
    context.Response.Headers[CorrelationIdContext.HeaderName] = correlationId;

    await next();
});

app.Use(async (context, next) =>
{
    // Same-origin only, no CDN, no frame embedding anywhere (plan section 6.1).
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
        "font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    context.Response.Headers["X-Frame-Options"] = "DENY";

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).WithName("GetHealthLive");
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" })).WithName("GetHealthReady");

app.MapOpenApi();

var api = app.MapGroup("/api/v1");
api.MapSessionEndpoints();
api.MapAntiforgeryEndpoints();
api.MapFileEndpoints();
api.MapBackupEndpoints();
api.MapDownloaderEndpoints();
api.MapRestoreEndpoints();
api.MapMaintenanceEndpoints();
api.MapArtifactEndpoints();
api.MapConfigurationEndpoints();
api.MapDeviceEndpoints();
api.MapServiceEndpoints();
api.MapOperationEndpoints();
api.MapActivityEndpoints();
api.MapEventEndpoints();

var webRootPath = app.Environment.WebRootPath;
if (!string.IsNullOrEmpty(webRootPath) && Directory.Exists(webRootPath))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();

    if (File.Exists(Path.Combine(webRootPath, "index.html")))
    {
        app.MapFallbackToFile("index.html");
    }
}

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
