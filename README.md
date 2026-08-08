# DBS POS Admin Tool

DBS POS Admin Tool is a .NET 10 **WinUI 3** desktop application for administering RMS+ Point-of-Sale installations at retail branches. It uses a Windows Store–style Fluent UI (Mica backdrop, `NavigationView` rail, light/dark runtime theming) on top of a clean-architecture C# codebase.

> **Programme status (2026-08-09):** Sessions 00-08 of the .NET 10 + Angular 22 migration are
> complete and preserved. The existing Angular/Agent architecture is retained, but standalone
> Angular expansion is frozen while POS is prepared for a possible RMS+ Support Hub integration.
> The retained WinUI application remains the compatibility/parity baseline. The repositories stay
> separate; no merge or integrated frontend is authorized yet.
>
> Active preparation documents: [`docs/POS_SUPPORT_HUB_MERGE_PREPARATION_PLAN.md`](docs/POS_SUPPORT_HUB_MERGE_PREPARATION_PLAN.md)
> and [`docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md`](docs/POS_SUPPORT_HUB_PREPARATION_SESSION_PROMPTS.md).

## Tech Stack

- .NET 10
- C# 13
- WinUI 3 (Windows App SDK) for the desktop UI — unpackaged, self-contained deployment
- CommunityToolkit.Mvvm for MVVM state and commands
- Microsoft.Data.SqlClient for SQL Server operations
- System.ServiceProcess.ServiceController for Windows service control
- FlaUI is no longer used — POS UI automation was removed along with the legacy DB Queries tab

## Architecture

```text
src/
  PosAdminTool.Domain/          Core models, enums, and service interfaces
  PosAdminTool.Application/     Backup, restore, cleanup, DB downloader, and use-case orchestration
  PosAdminTool.Infrastructure/  Encrypted config, SQL Server access, Windows services, connectivity,
                                 SMB backup repository, backup-trigger HTTP client
  PosAdminTool.Contracts/        Versioned Agent/Web DTOs and error/evidence contracts
  PosAdminTool.Agent/            Loopback ASP.NET Core host, authorization, operations, audit, and APIs
  PosAdminTool.Web/              Existing Angular 22 Branch Signal Desk implementation/reference
  PosAdminTool.WinUI/           NavigationView shell, XAML pages, controls, converters,
                                 design-token resource dictionaries, and view models

tests/
  PosAdminTool.Domain.Tests/
  PosAdminTool.Application.Tests/
  PosAdminTool.Infrastructure.Tests/
```

The UI is a `NavigationView` rail with five sections: Configuration, Services, Operations, DB Downloader, and Log. Long-running work is async and reports progress back to the UI via a shared `LogHub` activity console.

## Features and retained baseline

- Load and save RMS+ configuration at `~/.pos_admin_tool/config.json`
- Encrypt the SQL and RDB passwords at rest with PBKDF2-derived AES keys
- Import settings from RMS+ machine files under `C:\ProgramData\RMS_Plus` and `C:\Workspaces\DBS\RMS`
- Monitor RMS services and control start, stop, and restart actions
- Monitor API server TCP connectivity
- Back up selected RMS databases and config files into timestamped ZIP archives
- Restore SQL Server backups with logical file discovery and MOVE clauses
- Verify branch existence in the RMS database
- Provide guarded cleanup and branch reset operations
- **Download branch production DB backups from the main server** (see below)
- Switch between dark and light themes at runtime, independent of the OS setting

### DB Downloader

Triggers a production DB backup job for one or more branches and downloads the resulting ZIP archives once they're validated on the server.

**Workflow:**
1. Search/check the branches to include (the branch list is editable and stored in config), then set the API URL, RDB server IP/username/password, and the backup root folder on the server (e.g. `D:\DbBackups`).
2. **Trigger Backup Job** — `POST`s the selected branch codes to the configured API as a single batch call.
3. The app watches the server's backup root folder for a new batch folder (identified by the most recently *created* folder, not the highest serial number) and polls it for each branch's `<BranchCode>_<Serial>.zip`.
4. Each branch becomes independently downloadable as soon as its zip is detected and its size is stable across two polls (guards against downloading a still-writing file). Branches that never produce a zip within the configured timeout are marked timed out without blocking the rest of the batch.
5. Click **Download** next to a ready branch to save its zip locally (default: `Downloads\PosAdminTool_DbBackups`).

**Infrastructure dependency:** the app reads the server's backup folder over an SMB/UNC administrative share (`\\<server>\D$\...`) using the configured RDB credentials, via `PosAdminTool.Infrastructure.Smb.SmbBackupRepository`. This requires the client machine to have SMB (port 445) access to the server — RDP access alone is not sufficient. Folder access is abstracted behind `IBackupRepository`, so a future HTTP-based provider can replace SMB without touching the application logic in `DbDownloadService`.

## Requirements

- Windows 10/11
- .NET 10 SDK
- SQL Server access to the configured RMS databases
- Administrator privileges for service control and destructive maintenance operations
- SMB/UNC access to the RDB server's backup folder, for the DB Downloader feature

## Build

```powershell
dotnet restore
dotnet build PosAdminTool.sln -warnaserror
dotnet test PosAdminTool.sln --no-build
```

## Run

WinUI 3's self-contained, unpackaged deployment stages its native runtime dependencies only during **publish**, not a plain `dotnet build`. Always run the app from a published output:

```powershell
dotnet publish src\PosAdminTool.WinUI\PosAdminTool.WinUI.csproj -c Debug -r win-x64 --self-contained true
.\src\PosAdminTool.WinUI\bin\Debug\net10.0-windows10.0.19041.0\win-x64\publish\PosAdminTool.WinUI.exe
```

`run_app.cmd` launches the last-built Debug output with elevation prompts skipped (`POS_ADMIN_SKIP_ELEVATION=1`), for local development.

## Publish

To publish as a **standalone, single-file executable** that bundles all dependencies (including the .NET 10 runtime and Windows App SDK components):

```powershell
dotnet publish src\PosAdminTool.WinUI\PosAdminTool.WinUI.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true
```

The resulting executable is generated at:
`src\PosAdminTool.WinUI\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\PosAdminTool.WinUI.exe`

## Configuration Notes

The app keeps the previous config location for compatibility:

```text
%USERPROFILE%\.pos_admin_tool\config.json
```

Secrets (SQL password, RDB password) are written encrypted. The DB Downloader's known branch list and server settings are also persisted here, editable from the DB Downloader page.

The existing Angular/Agent replacement (`PosAdminTool.Agent`) does not use this file or its encryption scheme. It owns a separate, service-scoped configuration store under `%ProgramData%\DBS\PosAdminTool`, imports this file's non-secret settings once, and requires the SQL and RDB passwords to be re-entered rather than migrated — the legacy encryption key is bound to the interactive user and a Windows Service cannot reproduce it.

The Agent/Web implementation is now the accepted Sessions 00-08 baseline. POS preparation continues
only on backend, security, privileged-operation, portability, and merge-readiness concerns. The
final Angular shell, navigation, design system, branding, themes, and integrated POS routes belong
to RMS+ Support Hub and must not be duplicated here.
