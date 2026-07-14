using CommunityToolkit.Maui;
using Microsoft.Maui.LifecycleEvents;
using PosAdminTool.Application.Services;
using PosAdminTool.Application.UseCases;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Infrastructure.Automation;
using PosAdminTool.Infrastructure.Configuration;
using PosAdminTool.Infrastructure.Windows;
using PosAdminTool.Maui.Services;
using PosAdminTool.Maui.ViewModels;
using PosAdminTool.Maui.Views;

#if WINDOWS
using Microsoft.UI.Xaml.Media;
#endif

namespace PosAdminTool.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                events.AddWindows(windows =>
                {
                    windows.OnWindowCreated(window =>
                    {
                        window.SystemBackdrop = new MicaBackdrop();
                    });
                });
#endif
            });

        builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
        builder.Services.AddSingleton<ICryptoService, CryptoService>();
        builder.Services.AddSingleton<IServiceManager, WindowsServiceManager>();
        builder.Services.AddSingleton<IConnectivityMonitor, ConnectivityMonitor>();
        builder.Services.AddSingleton<IDatabaseService, SqlCmdExecutor>();
        builder.Services.AddSingleton<AdminPrivilegeManager>();

        builder.Services.AddTransient<BackupService>();
        builder.Services.AddTransient<RestoreService>();
        builder.Services.AddTransient<BranchVerificationService>();
        builder.Services.AddTransient<CleanupService>();
        builder.Services.AddTransient<DbQueryService>();
        builder.Services.AddTransient<IPosAutomationService, PosAutomationService>();

        builder.Services.AddTransient<TestConnectionUseCase>();
        builder.Services.AddTransient<ImportFromRmsUseCase>();
        builder.Services.AddTransient<RunOperationUseCase>();

        builder.Services.AddSingleton<LogHub>();
        builder.Services.AddTransient<ConfigurationViewModel>();
        builder.Services.AddTransient<ServicesViewModel>();
        builder.Services.AddTransient<OperationsViewModel>();
        builder.Services.AddTransient<DbQueriesViewModel>();
        builder.Services.AddTransient<LogViewModel>();

        builder.Services.AddTransient<ConfigurationPage>();
        builder.Services.AddTransient<ServicesPage>();
        builder.Services.AddTransient<OperationsPage>();
        builder.Services.AddTransient<DbQueriesPage>();
        builder.Services.AddTransient<LogPage>();

        return builder.Build();
    }
}
