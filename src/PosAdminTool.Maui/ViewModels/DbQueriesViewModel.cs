using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PosAdminTool.Application.Services;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;
using PosAdminTool.Maui.Services;

namespace PosAdminTool.Maui.ViewModels;

public sealed partial class DbQueriesViewModel(
    IConfigurationService configurationService,
    DbQueryService dbQueryService,
    IPosAutomationService posAutomationService,
    LogHub logHub)
    : BaseViewModel(logHub)
{
    private AppSettings _settings = new();
    private readonly List<string> _scannedCodes = [];

    public ObservableCollection<string> ClientNames { get; } = [];

    [ObservableProperty]
    private string? selectedClient;

    [ObservableProperty]
    private string server = "-";

    [ObservableProperty]
    private string user = "-";

    [ObservableProperty]
    private string database = "-";

    [ObservableProperty]
    private string passwordMask = "-";

    [ObservableProperty]
    private string queryCount = "100";

    [ObservableProperty]
    private string resultsText = string.Empty;

    [ObservableProperty]
    private bool hasScannedCodes;

    [RelayCommand]
    public async Task LoadAsync()
    {
        _settings = await configurationService.LoadAsync();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ClientNames.Clear();
            foreach (var name in _settings.ClientRemoteDbs.Keys.OrderBy(static name => name))
            {
                ClientNames.Add(name);
            }

            SelectedClient ??= ClientNames.FirstOrDefault();
            if (ClientNames.Count == 0)
            {
                ResultsText = "No client database profiles are configured.";
            }
        });
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        var config = CurrentConfig();
        if (config is null || SelectedClient is null)
        {
            StatusMessage = "No client selected";
            return;
        }

        await RunBusyAsync("Testing remote database...", async () =>
        {
            var result = await dbQueryService.TestConnectionAsync(SelectedClient, config);
            LogHub.AddResult("DB query connection", result);
            StatusMessage = result.Success ? $"Connected to {config.Server}" : string.Join(Environment.NewLine, result.Errors);
        });
    }

    [RelayCommand]
    private async Task RunQueryAsync()
    {
        var config = CurrentConfig();
        if (config is null || SelectedClient is null)
        {
            StatusMessage = "No client selected";
            return;
        }

        if (!int.TryParse(QueryCount, out var count) || count < 1)
        {
            StatusMessage = "Enter a valid number";
            return;
        }

        try
        {
            await RunBusyAsync($"Querying {count} random code(s)...", async () =>
            {
                var (result, codes) = await dbQueryService.GetRandomScannedCodesAsync(SelectedClient, config, count);
                LogHub.AddResult("Random scanned codes", result);
                _scannedCodes.Clear();
                _scannedCodes.AddRange(codes);
                HasScannedCodes = _scannedCodes.Count > 0;
                ResultsText = HasScannedCodes ? string.Join(Environment.NewLine, _scannedCodes) : "(no results)";
                StatusMessage = result.Success ? $"Found {_scannedCodes.Count} code(s)" : string.Join(Environment.NewLine, result.Errors);
            });
        }
        finally
        {
            AddToCartCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddToCart))]
    private async Task AddToCartAsync()
    {
        try
        {
            await RunBusyAsync("Adding items to POS cart...", async () =>
            {
                var progress = new Progress<string>(message =>
                {
                    ResultsText = string.IsNullOrWhiteSpace(ResultsText)
                        ? message
                        : string.Concat(ResultsText, Environment.NewLine, message);
                    Log(message);
                });

                await posAutomationService.AddItemsToCartAsync(_scannedCodes, progress);
                StatusMessage = $"Added {_scannedCodes.Count} item(s) to cart";
            });
        }
        finally
        {
            AddToCartCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnSelectedClientChanged(string? value)
    {
        var config = CurrentConfig();
        Server = config?.Server ?? "-";
        User = config?.User ?? "-";
        Database = config?.Database ?? "-";
        PasswordMask = string.IsNullOrWhiteSpace(config?.Password) ? "-" : "********";
        StatusMessage = string.Empty;
    }

    private bool CanAddToCart()
    {
        return HasScannedCodes && !IsBusy;
    }

    private ClientDbConfig? CurrentConfig()
    {
        return SelectedClient is not null && _settings.ClientRemoteDbs.TryGetValue(SelectedClient, out var config)
            ? config
            : null;
    }
}
