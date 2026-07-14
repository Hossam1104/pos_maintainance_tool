using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using PosAdminTool.Domain.Interfaces;

namespace PosAdminTool.Infrastructure.Automation;

using FlaUIApplication = FlaUI.Core.Application;

public sealed class PosAutomationService(IConfigurationService configurationService) : IPosAutomationService
{
    public async Task AddItemsToCartAsync(IReadOnlyList<string> scannedCodes, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (scannedCodes.Count == 0)
        {
            throw new InvalidOperationException("No scanned codes were provided.");
        }

        var settings = await configurationService.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(settings.PosExecutablePath) || !File.Exists(settings.PosExecutablePath))
        {
            throw new FileNotFoundException("POS executable was not found.", settings.PosExecutablePath);
        }

        if (string.IsNullOrWhiteSpace(settings.PosUsername) || string.IsNullOrWhiteSpace(settings.PosPassword))
        {
            throw new InvalidOperationException("POS credentials must be configured before automation can run.");
        }

        await Task.Run(() => RunAutomation(settings.PosExecutablePath, settings.PosUsername, settings.PosPassword, scannedCodes, progress, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    private static FlaUIApplication ResolveApplication(string posExecutablePath, IProgress<string>? progress)
    {
        var processName = Path.GetFileNameWithoutExtension(posExecutablePath);
        var existing = Process.GetProcessesByName(processName).FirstOrDefault();
        if (existing is not null)
        {
            progress?.Report("Attaching to running POS application...");
            return FlaUIApplication.Attach(existing);
        }

        progress?.Report("Launching POS application...");
        return FlaUIApplication.Launch(posExecutablePath);
    }

    private static void RunAutomation(
        string posExecutablePath,
        string username,
        string password,
        IReadOnlyList<string> scannedCodes,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("Preparing POS application...");
        using var application = ResolveApplication(posExecutablePath, progress);
        using var automation = new UIA3Automation();

        Sleep(TimeSpan.FromSeconds(5), cancellationToken);
        progress?.Report("Waiting for login screen...");
        var mainWindow = application.GetMainWindow(automation, TimeSpan.FromSeconds(30))
            ?? throw new InvalidOperationException("Could not find POS window.");

        progress?.Report("Logging in...");
        if (!TryLoginWithEditControls(mainWindow, username, password, progress, cancellationToken)
            && !TryLoginWithNumericKeypad(mainWindow, username, password, progress, cancellationToken)
            && !TryLoginWithKeystrokes(mainWindow, username, password, progress, cancellationToken))
        {
            throw new InvalidOperationException("Login automation failed.");
        }

        Sleep(TimeSpan.FromSeconds(5), cancellationToken);
        mainWindow = application.GetMainWindow(automation, TimeSpan.FromSeconds(15)) ?? mainWindow;

        progress?.Report("Opening invoice...");
        ClickButtonContaining(mainWindow, ["Open", "Invoice"], TimeSpan.FromSeconds(15));
        Sleep(TimeSpan.FromSeconds(2), cancellationToken);

        progress?.Report("Selecting regular invoice...");
        ClickButtonContaining(mainWindow, ["Regular"], TimeSpan.FromSeconds(15));
        Sleep(TimeSpan.FromSeconds(3), cancellationToken);

        progress?.Report($"Adding {scannedCodes.Count} item(s) to cart...");
        for (var index = 0; index < scannedCodes.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var code = scannedCodes[index];
            var barcodeField = FindEditContaining(mainWindow, ["Barcode", "Scan", "Code", "Search"], TimeSpan.FromSeconds(10))
                ?? mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit)).FirstOrDefault()
                ?? throw new InvalidOperationException("Could not find the barcode field.");

            barcodeField.Focus();
            SetText(barcodeField, code);
            Keyboard.Press(VirtualKeyShort.ENTER);
            Sleep(TimeSpan.FromMilliseconds(1500), cancellationToken);
            progress?.Report($"[{index + 1}/{scannedCodes.Count}] Added: {code}");
        }

        progress?.Report($"Finished adding {scannedCodes.Count} item(s).");
    }

    private static bool TryLoginWithEditControls(AutomationElement window, string username, string password, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        try
        {
            var edits = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
            progress?.Report($"Found {edits.Length} edit control(s).");
            if (edits.Length < 2)
            {
                return false;
            }

            SetText(edits[0], username);
            Sleep(TimeSpan.FromMilliseconds(500), cancellationToken);
            SetText(edits[1], password);
            Sleep(TimeSpan.FromMilliseconds(500), cancellationToken);
            ClickButtonByName(window, "Enter", TimeSpan.FromSeconds(10));
            progress?.Report("Login submitted with edit controls.");
            return true;
        }
        catch (Exception ex)
        {
            progress?.Report($"Edit-control login failed: {ex.Message}");
            return false;
        }
    }

    private static bool TryLoginWithNumericKeypad(AutomationElement window, string username, string password, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        try
        {
            var edits = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
            if (edits.Length < 2)
            {
                return false;
            }

            edits[0].Focus();
            foreach (var character in username)
            {
                ClickButtonByName(window, character.ToString(), TimeSpan.FromSeconds(5));
                Sleep(TimeSpan.FromMilliseconds(150), cancellationToken);
            }

            edits[1].Focus();
            foreach (var character in password)
            {
                ClickButtonByName(window, character.ToString(), TimeSpan.FromSeconds(5));
                Sleep(TimeSpan.FromMilliseconds(150), cancellationToken);
            }

            ClickButtonByName(window, "Enter", TimeSpan.FromSeconds(10));
            progress?.Report("Login submitted with numeric keypad.");
            return true;
        }
        catch (Exception ex)
        {
            progress?.Report($"Numeric-keypad login failed: {ex.Message}");
            return false;
        }
    }

    private static bool TryLoginWithKeystrokes(AutomationElement window, string username, string password, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        try
        {
            window.Focus();
            Sleep(TimeSpan.FromMilliseconds(500), cancellationToken);
            Keyboard.Type(username);
            Keyboard.Press(VirtualKeyShort.TAB);
            Keyboard.Type(password);
            Keyboard.Press(VirtualKeyShort.ENTER);
            progress?.Report("Login submitted with keystrokes.");
            return true;
        }
        catch (Exception ex)
        {
            progress?.Report($"Keystroke login failed: {ex.Message}");
            return false;
        }
    }

    private static void SetText(AutomationElement element, string value)
    {
        element.Focus();
        try
        {
            element.AsTextBox().Text = value;
        }
        catch
        {
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type(value);
        }
    }

    private static void ClickButtonByName(AutomationElement window, string name, TimeSpan timeout)
    {
        var until = DateTimeOffset.UtcNow.Add(timeout);
        do
        {
            var button = window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName(name)));
            if (button is not null)
            {
                button.AsButton().Invoke();
                return;
            }

            Thread.Sleep(100);
        }
        while (DateTimeOffset.UtcNow < until);

        throw new InvalidOperationException($"Button '{name}' was not found.");
    }

    private static void ClickButtonContaining(AutomationElement window, IReadOnlyList<string> tokens, TimeSpan timeout)
    {
        var until = DateTimeOffset.UtcNow.Add(timeout);
        do
        {
            var button = window
                .FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                .FirstOrDefault(element => tokens.All(token => element.Name.Contains(token, StringComparison.OrdinalIgnoreCase)));

            if (button is not null)
            {
                button.AsButton().Invoke();
                return;
            }

            Thread.Sleep(100);
        }
        while (DateTimeOffset.UtcNow < until);

        throw new InvalidOperationException($"Button containing '{string.Join(' ', tokens)}' was not found.");
    }

    private static AutomationElement? FindEditContaining(AutomationElement window, IReadOnlyList<string> tokens, TimeSpan timeout)
    {
        var until = DateTimeOffset.UtcNow.Add(timeout);
        do
        {
            var edit = window
                .FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
                .FirstOrDefault(element => tokens.Any(token => element.Name.Contains(token, StringComparison.OrdinalIgnoreCase)));

            if (edit is not null)
            {
                return edit;
            }

            Thread.Sleep(100);
        }
        while (DateTimeOffset.UtcNow < until);

        return null;
    }

    private static void Sleep(TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.WaitHandle.WaitOne(delay);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
