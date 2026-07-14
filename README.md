# DBS POS Admin Tool

DBS POS Admin Tool is a .NET 10 MAUI desktop application for administering RMS+ Point-of-Sale installations at retail branches. It replaces the previous Python/PySide6 implementation with a Windows-first, clean-architecture C# codebase.

## Tech Stack

- .NET 10
- C# 13
- .NET MAUI for the Windows desktop UI
- CommunityToolkit.Mvvm for MVVM state and commands
- Microsoft.Data.SqlClient for SQL Server operations
- System.ServiceProcess.ServiceController for Windows service control
- FlaUI UIA3 for POS UI automation

## Architecture

```text
src/
  PosAdminTool.Domain/          Core models, enums, and service interfaces
  PosAdminTool.Application/     Backup, restore, cleanup, DB query, and use-case orchestration
  PosAdminTool.Infrastructure/  Encrypted config, SQL Server access, Windows services, connectivity, FlaUI
  PosAdminTool.Maui/            Shell tabs, XAML pages, controls, converters, styles, and view models

tests/
  PosAdminTool.Domain.Tests/
  PosAdminTool.Application.Tests/
  PosAdminTool.Infrastructure.Tests/
```

The UI is split into five Shell tabs: Configuration, Services, Operations, DB Queries, and Log. Long-running work is async and reports progress back to the UI.

## Features

- Load and save RMS+ configuration at `~/.pos_admin_tool/config.json`
- Encrypt SQL, POS, and remote client database passwords with PBKDF2-derived AES keys
- Import settings from RMS+ machine files under `C:\ProgramData\RMS_Plus` and `C:\Workspaces\DBS\RMS`
- Monitor RMS services and control start, stop, and restart actions
- Monitor API server TCP connectivity
- Back up selected RMS databases and config files into timestamped ZIP archives
- Restore SQL Server backups with logical file discovery and MOVE clauses
- Verify branch existence in the RMS database
- Run random `ScannedCode` queries against configured remote client databases
- Automate POS login, invoice opening, and barcode entry through FlaUI
- Provide guarded cleanup and branch reset operations
- Switch between dark and light MAUI themes

## Requirements

- Windows 10/11
- .NET 10 SDK
- .NET MAUI Windows workload
- SQL Server access to the configured RMS databases
- Administrator privileges for service control and destructive maintenance operations

Install the MAUI workload if it is missing:

```powershell
dotnet workload install maui-windows
```

## Build

```powershell
dotnet restore
dotnet build PosAdminTool.sln -warnaserror
dotnet test PosAdminTool.sln --no-build
```

## Publish

To publish as a folder containing the executable and its dependencies:

```powershell
dotnet publish src\PosAdminTool.Maui\PosAdminTool.Maui.csproj -c Release -f net10.0-windows10.0.19041.0
```

To publish as a **standalone, single-file executable** that bundles all dependencies (including .NET 10 runtime and Windows App SDK components) and can run on any device:

```powershell
dotnet publish src\PosAdminTool.Maui\PosAdminTool.Maui.csproj -c Release -f net10.0-windows10.0.19041.0 -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:WindowsAppSDKSelfContained=true
```

The resulting standalone executable will be generated at:
`src\PosAdminTool.Maui\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\PosAdminTool.Maui.exe`


## Configuration Notes

The app keeps the previous config location for compatibility:

```text
%USERPROFILE%\.pos_admin_tool\config.json
```

Secrets are written encrypted. Remote client DB profiles are intentionally empty by default; add client server, user, database, and password values through the config file or future profile-management UI before using the DB Queries tab.
