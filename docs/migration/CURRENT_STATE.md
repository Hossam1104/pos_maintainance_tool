# Current-state baseline

Audit date: 2026-07-26. This is an evidence-backed snapshot taken before any Agent or Angular project exists. Source citations use the repository state audited in Session 00.

## Measured inventory

| Project | C# files | C# lines | XAML files | Target / role |
| --- | ---: | ---: | ---: | --- |
| `PosAdminTool.Domain` | 21 | 419 | 0 | `net10.0`; models, enums, interfaces |
| `PosAdminTool.Application` | 8 | 1,089 | 0 | `net10.0`; backup, restore, cleanup, downloader, use cases |
| `PosAdminTool.Infrastructure` | 11 | 1,115 | 0 | `net10.0-windows10.0.19041.0`; SQL, Windows services, SMB, encrypted configuration |
| `PosAdminTool.WinUI` | 21 | 1,575 | 11 | `net10.0-windows10.0.19041.0`; shell, five views, controls, resources, view models |
| **Total** | **61** | **4,198** | **11** | 4 source projects |

Counts are measured from source paths with `rg --files ... -g '*.cs'`; the 11 XAML files include five routable views, the main window, app resources, two reusable controls, and two resource dictionaries. There is no Agent project, Angular workspace, Node lockfile, `global.json`, or CI definition in the audited tree.

## Feature and dependency inventory

| Area | Current evidence | Dependency / coupling | Migration treatment |
| --- | --- | --- | --- |
| Configuration | The configuration view model loads, imports RMS values, verifies a branch, tests SQL, and saves settings ([`ConfigurationViewModel.cs`](../../src/PosAdminTool.WinUI/ViewModels/ConfigurationViewModel.cs:54)); the page exposes SQL, identity, path, database, and service inputs ([`ConfigurationPage.xaml`](../../src/PosAdminTool.WinUI/Views/ConfigurationPage.xaml:77)). | `IConfigurationService`, `IDatabaseService`; user-profile JSON file ([`ConfigurationService.cs`](../../src/PosAdminTool.Infrastructure/Configuration/ConfigurationService.cs:23)). | Settings and device routes; server-side secret handling and browse handles. |
| Windows services | Configured services are polled every five seconds and expose refresh/start/stop/restart ([`ServicesViewModel.cs`](../../src/PosAdminTool.WinUI/ViewModels/ServicesViewModel.cs:56)); cards bind each action ([`ServicesPage.xaml`](../../src/PosAdminTool.WinUI/Views/ServicesPage.xaml:83)). | `System.ServiceProcess.ServiceController`, `sc.exe`, `DispatcherQueueTimer`. | Agent-only service adapter; REST + SSE. |
| Backup | Five selectable payloads are composed into a timestamped ZIP ([`OperationsPage.xaml`](../../src/PosAdminTool.WinUI/Views/OperationsPage.xaml:45); [`BackupService.cs`](../../src/PosAdminTool.Application/Services/BackupService.cs:128)). | SQL adapter, local host paths, ZIP APIs; opens Explorer after completion ([`BackupService.cs`](../../src/PosAdminTool.Application/Services/BackupService.cs:270)). | `/backups`; managed destination browse handle and artifact catalog replace Explorer. |
| Restore | Full/database-only/config-only choice and raw ZIP/DB-path inputs ([`OperationsPage.xaml`](../../src/PosAdminTool.WinUI/Views/OperationsPage.xaml:96)); extraction and overwrite happen in the application service ([`RestoreService.cs`](../../src/PosAdminTool.Application/Services/RestoreService.cs:47)). | SQL adapter, ZIP APIs, host paths. | `/restore`; server preview and archive/path policy before execution. |
| Cleanup and branch reset | Client checkbox gates both commands ([`OperationsViewModel.cs`](../../src/PosAdminTool.WinUI/ViewModels/OperationsViewModel.cs:126)); UI describes permanent deletion/reset ([`OperationsPage.xaml`](../../src/PosAdminTool.WinUI/Views/OperationsPage.xaml:126)). | `CleanupService`, SQL and local file system. | `/maintenance`; policy, preview, one-time challenge, typed confirmation, audit. |
| DB Downloader | Sends a configured API call, observes SMB folders, validates stable ZIP size, and downloads per branch ([`DbDownloadService.cs`](../../src/PosAdminTool.Application/Services/DbDownloadService.cs:10); [`DbDownloaderPage.xaml`](../../src/PosAdminTool.WinUI/Views/DbDownloaderPage.xaml:30)). | `HttpClient`, SMB `mpr.dll`, RDB credentials, in-memory UI job state. | `/downloads`; opaque artifact IDs and Agent-held credentials only. |
| Activity log | In-memory 1,000-entry timestamped console with a clear action ([`LogHub.cs`](../../src/PosAdminTool.WinUI/Services/LogHub.cs:8); [`LogPage.xaml`](../../src/PosAdminTool.WinUI/Views/LogPage.xaml:25)). | `DispatcherQueue`; raw operation messages/errors can be displayed. | `/activity`; sanitized operation/audit timeline and correlation IDs. |
| Shell and theme | Five NavigationView areas and runtime light/dark toggle ([`MainWindow.xaml`](../../src/PosAdminTool.WinUI/MainWindow.xaml:33); [`MainWindow.xaml`](../../src/PosAdminTool.WinUI/MainWindow.xaml:43)). | WinUI/XAML, process-wide elevation at startup. | Responsive Angular shell and new overview; elevation becomes Agent installation/runtime concern. |

Package versions are currently non-deterministic: `Microsoft.Extensions.Logging.Abstractions 10.*` in Application ([`PosAdminTool.Application.csproj`](../../src/PosAdminTool.Application/PosAdminTool.Application.csproj:12)); `Microsoft.Data.SqlClient 6.*`, `Microsoft.Extensions.Logging.Abstractions 10.*`, and `System.ServiceProcess.ServiceController 10.*` in Infrastructure ([`PosAdminTool.Infrastructure.csproj`](../../src/PosAdminTool.Infrastructure/PosAdminTool.Infrastructure.csproj:14)); and Windows App SDK, CommunityToolkit, and Microsoft.Extensions wildcards in WinUI ([`PosAdminTool.WinUI.csproj`](../../src/PosAdminTool.WinUI/PosAdminTool.WinUI.csproj:28)). The shared language version is C# 13 ([`Directory.Build.props`](../../Directory.Build.props:3)).

## Section 3.4 re-audit

All fifteen plan findings still hold. Citations below supersede stale line references in the plan; no runtime behaviour was changed in this session.

| # | Re-verified finding | Current evidence | Status |
| ---: | --- | --- | --- |
| 1 | `SqlPassword` has a non-empty hard-coded default. | [`AppSettings.cs`](../../src/PosAdminTool.Domain/Models/AppSettings.cs:13) | Confirmed |
| 2 | SQL password is encrypted/decrypted; RDB password is not. | [`ConfigurationService.cs`](../../src/PosAdminTool.Infrastructure/Configuration/ConfigurationService.cs:112), [`DbDownloaderSettings.cs`](../../src/PosAdminTool.Domain/Models/DbDownloaderSettings.cs:11) | Confirmed |
| 3 | Legacy cipher key material depends on host and interactive user identity. | [`CryptoService.cs`](../../src/PosAdminTool.Infrastructure/Configuration/CryptoService.cs:128), [`CryptoService.cs`](../../src/PosAdminTool.Infrastructure/Configuration/CryptoService.cs:153) | Confirmed |
| 4 | Downloader default contains an environment-specific HTTP endpoint. | [`DbDownloaderSettings.cs`](../../src/PosAdminTool.Domain/Models/DbDownloaderSettings.cs:5) | Confirmed |
| 5 | Cleanup expands variables then recursively deletes configured paths without an allowlist/policy/preview. | [`CleanupService.cs`](../../src/PosAdminTool.Application/Services/CleanupService.cs:31) | Confirmed |
| 6 | Destructive controls rely on a client-side `RiskAccepted` checkbox. | [`OperationsViewModel.cs`](../../src/PosAdminTool.WinUI/ViewModels/OperationsViewModel.cs:49), [`OperationsViewModel.cs`](../../src/PosAdminTool.WinUI/ViewModels/OperationsViewModel.cs:126), [`OperationsPage.xaml`](../../src/PosAdminTool.WinUI/Views/OperationsPage.xaml:135) | Confirmed |
| 7 | Downloader accepts configured HTTP endpoint, SMB host, root, and credentials without Agent policy. | [`DbDownloadService.cs`](../../src/PosAdminTool.Application/Services/DbDownloadService.cs:18), [`DbDownloadService.cs`](../../src/PosAdminTool.Application/Services/DbDownloadService.cs:106) | Confirmed |
| 8 | Restore extracts ZIPs without entry-count, size, ratio, traversal, file-type, or upload limits. | [`RestoreService.cs`](../../src/PosAdminTool.Application/Services/RestoreService.cs:42), [`RestoreService.cs`](../../src/PosAdminTool.Application/Services/RestoreService.cs:47) | Confirmed |
| 9 | Downloader work lives in the WinUI process and has no operation ID/cancellation/concurrency contract. | [`DbDownloaderViewModel.cs`](../../src/PosAdminTool.WinUI/ViewModels/DbDownloaderViewModel.cs:130), [`DbDownloadService.cs`](../../src/PosAdminTool.Application/Services/DbDownloadService.cs:10) | Confirmed |
| 10 | Existing logs and results can include raw operation errors; no HTTP redaction contract exists. | [`LogHub.cs`](../../src/PosAdminTool.WinUI/Services/LogHub.cs:49), [`DbDownloadService.cs`](../../src/PosAdminTool.Application/Services/DbDownloadService.cs:117) | Confirmed |
| 11 | Only 14 test methods exist; no listed safety/API/streaming coverage exists. | Test inventory below | Confirmed |
| 12 | Four project files use wildcard NuGet versions. | [`PosAdminTool.Application.csproj`](../../src/PosAdminTool.Application/PosAdminTool.Application.csproj:12), [`PosAdminTool.Infrastructure.csproj`](../../src/PosAdminTool.Infrastructure/PosAdminTool.Infrastructure.csproj:14), [`PosAdminTool.WinUI.csproj`](../../src/PosAdminTool.WinUI/PosAdminTool.WinUI.csproj:28) | Confirmed |
| 13 | .NET 10 SDK projects deliberately pin C# 13. | [`Directory.Build.props`](../../Directory.Build.props:3) | Confirmed |
| 14 | SMB uses `WNetAddConnection2`, whose connection is in the caller's logon session. | [`SmbConnectionScope.cs`](../../src/PosAdminTool.Infrastructure/Smb/SmbConnectionScope.cs:19), [`SmbConnectionScope.cs`](../../src/PosAdminTool.Infrastructure/Smb/SmbConnectionScope.cs:66) | Confirmed |
| 15 | Backup attempts to open Explorer with `Process.Start`. | [`BackupService.cs`](../../src/PosAdminTool.Application/Services/BackupService.cs:270) | Confirmed |

## Existing test inventory

The suite declares 14 test methods: 13 `[Fact]` methods and one `[Theory]` method. The theory has four inline-data rows, so the required Release run executes 17 test cases; the plan/runbook's “14 tests” wording refers to the declared methods, not the runner total.

| Project | Test case |
| --- | --- |
| `PosAdminTool.Application.Tests` | `BranchVerificationServiceTests.VerifyAsyncRejectsEmptyBranchBeforeDatabaseCall` |
| `PosAdminTool.Application.Tests` | `DbDownloadServiceTests.RunAsyncPicksMostRecentlyCreatedFolderNotHighestSerial` |
| `PosAdminTool.Application.Tests` | `DbDownloadServiceTests.RunAsyncIgnoresChunkFilesAndOnlyMatchesExactZipName` |
| `PosAdminTool.Application.Tests` | `DbDownloadServiceTests.RunAsyncTracksBranchesIndependentlyWithinABatch` |
| `PosAdminTool.Application.Tests` | `DbDownloadServiceTests.RunAsyncTimesOutWhenBatchFolderNeverAppears` |
| `PosAdminTool.Application.Tests` | `DbDownloadServiceTests.DownloadAsyncMarksItemDownloadedAndUsesRepository` |
| `PosAdminTool.Application.Tests` | `ImportFromRmsUseCaseTests.ExecuteAsyncReadsReleaseNumberFromRmsInfo` |
| `PosAdminTool.Application.Tests` | `ImportFromRmsUseCaseTests.ExecuteAsyncReadsClientNameWithoutJsonQuotes` |
| `PosAdminTool.Domain.Tests` | `DatabaseResolverTests.ResolveBranchDatabasePrefersBranchNamedDatabase` |
| `PosAdminTool.Domain.Tests` | `DatabaseResolverTests.ResolvePrimaryDatabaseUsesBranchDatabase` |
| `PosAdminTool.Domain.Tests` | `MultilineTextTests.SplitLinesHandlesMixedEditorLineEndings` |
| `PosAdminTool.Domain.Tests` | `OperationResultTests.FinalizeSuccessMarksResultSuccessful` |
| `PosAdminTool.Infrastructure.Tests` | `ConnectivityMonitorTests.ParseHostPortResolvesDefaultPorts` (4 inline-data cases) |
| `PosAdminTool.Infrastructure.Tests` | `CryptoServiceTests.EncryptDecryptRoundTripsSecret` |

Session 00 verification output is recorded verbatim in [SESSION_LOG.md](SESSION_LOG.md).
