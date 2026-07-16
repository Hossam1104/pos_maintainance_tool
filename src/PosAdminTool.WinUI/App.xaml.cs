using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using PosAdminTool.Application.Services;
using PosAdminTool.Application.UseCases;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Infrastructure.Configuration;
using PosAdminTool.Infrastructure.Http;
using PosAdminTool.Infrastructure.Smb;
using PosAdminTool.Infrastructure.Windows;
using PosAdminTool.WinUI.Services;
using PosAdminTool.WinUI.ViewModels;

namespace PosAdminTool.WinUI;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;
    private readonly bool _exitAfterElevation;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => LogCrash("AppDomain", e.ExceptionObject as Exception);
        UnhandledException += (s, e) => { LogCrash("Xaml", e.Exception); e.Handled = true; };
        TaskScheduler.UnobservedTaskException += (s, e) => LogCrash("Task", e.Exception);

        InitializeComponent();
        Services = BuildServiceProvider();

        var adminPrivilegeManager = Services.GetRequiredService<AdminPrivilegeManager>();
        if (!adminPrivilegeManager.IsAdministrator() && adminPrivilegeManager.RequestAdministrator())
        {
            _exitAfterElevation = true;
        }
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "pos_admin_crash.txt");
            var msg = $"[{DateTime.Now}] Source: {source}\nException: {ex?.ToString() ?? "null"}\nInnerException: {ex?.InnerException?.ToString() ?? "null"}\n\n";
            File.AppendAllText(path, msg);
        }
        catch {}
    }

    public static IServiceProvider Services { get; private set; } = null!;

    public static T Resolve<T>()
        where T : notnull
    {
        return Services.GetRequiredService<T>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_exitAfterElevation)
        {
            Exit();
            return;
        }

        _window = new MainWindow();
        _window.Activate();
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ICryptoService, CryptoService>();
        services.AddSingleton<IServiceManager, WindowsServiceManager>();
        services.AddSingleton<IConnectivityMonitor, ConnectivityMonitor>();
        services.AddSingleton<IDatabaseService, SqlCmdExecutor>();
        services.AddSingleton<AdminPrivilegeManager>();
        services.AddSingleton(new HttpClient());
        services.AddSingleton<IBackupApiClient, BackupApiClient>();
        services.AddSingleton<IBackupRepository, SmbBackupRepository>();

        services.AddTransient<BackupService>();
        services.AddTransient<RestoreService>();
        services.AddTransient<BranchVerificationService>();
        services.AddTransient<CleanupService>();
        services.AddTransient<DbDownloadService>();

        services.AddTransient<TestConnectionUseCase>();
        services.AddTransient<ImportFromRmsUseCase>();
        services.AddTransient<RunOperationUseCase>();

        services.AddSingleton<LogHub>();
        services.AddSingleton<ThemeService>();

        services.AddTransient<ConfigurationViewModel>();
        services.AddTransient<ServicesViewModel>();
        services.AddTransient<OperationsViewModel>();
        services.AddTransient<DbDownloaderViewModel>();
        services.AddTransient<LogViewModel>();

        return services.BuildServiceProvider();
    }
}
