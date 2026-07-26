# Feature parity matrix

Target endpoints are planned `/api/v1` contracts, not implemented endpoints. They name later-session work and never imply that a browser sends paths, service names, SMB details, or credentials. “Retained” means WinUI remains the working implementation until explicit Session 14 cutover approval.

| Current feature / command | Current entry point | Target API endpoint | Angular route | Safety | Tests required | Cutover status |
| --- | --- | --- | --- | --- | --- | --- |
| Navigate configuration | `MainWindow.xaml` / `configuration` item | `GET /api/v1/configuration`, `GET /api/v1/device` | `/settings`, `/device` | read | Route, auth, no-secret contract | Retained; split planned |
| Load configuration | `ConfigurationViewModel.LoadAsync` | `GET /api/v1/configuration` | `/settings` | read | Contract redaction; legacy non-secret import fixture | Retained |
| Save configuration | `ConfigurationViewModel.SaveConfigAsync` | `PUT /api/v1/configuration` | `/settings` | mutating | Validation, antiforgery, keep/replace/clear secret tests | Retained |
| Import RMS+ values | `ConfigurationViewModel.ImportFromRmsAsync` | `POST /api/v1/configuration/import-legacy` | `/settings` | mutating | Idempotent non-secret import; no source-config modification | Retained |
| Test SQL connection | `ConfigurationViewModel.TestDbAsync` | `POST /api/v1/configuration/sql-connection-test` | `/settings` | read | Fake SQL success/failure; sanitized error | Retained |
| Verify branch | `ConfigurationViewModel.VerifyBranchAsync` | `POST /api/v1/device/branch-verification` | `/device` | read | Empty branch, fake SQL result, sanitized error | Retained |
| View connectivity / identity | `ConfigurationPage.xaml` bindings | `GET /api/v1/device` | `/device` | read | Fresh/stale/offline state, timestamp UTC | New split planned |
| Toggle theme | `MainWindow.OnToggleThemeClicked` | none (client preference only) | shell | mutating | Preference persistence; contrast/reduced-motion snapshots | Retained concept; redesign planned |
| Navigate services | `MainWindow.xaml` / `services` item | `GET /api/v1/services` | `/services` | read | Route and local-admin authorization | Retained |
| Refresh service status | `ServicesViewModel.RefreshAsync` | `GET /api/v1/services` | `/services` | read | Adapter fixture, SSE/REST reconciliation | Retained |
| Start service | `ServiceRowViewModel.StartAsync` | `POST /api/v1/services/{serviceId}/actions/start` | `/services` | mutating | Authorization, invalid/conflicting/timeout cases, disposable-service fixture | Retained |
| Stop service | `ServiceRowViewModel.StopAsync` | `POST /api/v1/services/{serviceId}/actions/stop` | `/services` | mutating | Authorization, invalid/conflicting/timeout cases, disposable-service fixture | Retained |
| Restart service | `ServiceRowViewModel.RestartAsync` | `POST /api/v1/services/{serviceId}/actions/restart` | `/services` | mutating | Double-click prevention, authorization, outcome fixture | Retained |
| Navigate operations / choose backup items | `OperationsPage.xaml` / selection controls | `GET /api/v1/backups/options` | `/backups` | read | Component selection and route state | Retained; split planned |
| Select all backup items | `OperationsViewModel.SelectAllBackupItems` | none (client selection state) | `/backups` | mutating | UI selection test | Retained concept |
| Create backup | `OperationsViewModel.RunBackupAsync` / `BackupService.BackupAsync` | `POST /api/v1/backups` | `/backups` | mutating | Fake SQL/files, cancellation, partial failure, artifact metadata, no raw path in request | Retained |
| Browse backup destination | raw configured folder in `BackupService` | `POST /api/v1/browse-sessions`, `GET /api/v1/browse-sessions/{browseId}` | `/backups` | read | Root allowlist, traversal/absolute/reparse rejection | New safety replacement |
| Get/download backup artifact | Explorer open in `BackupService.TryOpenFolder` | `GET /api/v1/artifacts/{artifactId}`, `GET /api/v1/artifacts/{artifactId}/content` | `/backups` | read | Artifact authorization, streaming, checksum/size, no Explorer process | Replaces Explorer affordance |
| Restore preview | `OperationsViewModel.RestoreDatabaseAsync` / `RestoreService.RestoreAsync` | `POST /api/v1/restores/previews` | `/restore` | read | ZIP traversal/bomb/limit/type/ambiguous-backup rejection | New mandatory gate |
| Execute restore | same | `POST /api/v1/restores/{previewId}/execute` | `/restore` | mutating | Fresh preview, all three modes, fake SQL/files, cancellation | Retained with safety redesign |
| Select restore source | raw ZIP/DB-path text boxes in `OperationsPage.xaml` | `POST /api/v1/uploads` or browse-session endpoints | `/restore` | mutating | Upload bounds and opaque browse-handle tests | Replaces raw paths |
| Cleanup preview | `OperationsViewModel.CleanupAsync` / `CleanupService.CleanupFilesAsync` | `POST /api/v1/maintenance/cleanup/previews` | `/maintenance` | destructive | Protected-root/traversal/UNC/reparse tests; preview contents | New mandatory gate |
| Execute cleanup | same | `POST /api/v1/maintenance/cleanup/challenges`, `POST /api/v1/maintenance/cleanup/execute` | `/maintenance` | destructive | Expiry/reuse/typed-confirmation/recompute/audit/no-secret tests | Retained with safety redesign |
| Reset branch preview | `OperationsViewModel.ResetBranchDataAsync` / `CleanupService.ResetBranchDataAsync` | `POST /api/v1/maintenance/branch-reset/previews` | `/maintenance` | destructive | Branch/table preview and fake-SQL policy tests | New mandatory gate |
| Execute branch reset | same | `POST /api/v1/maintenance/branch-reset/challenges`, `POST /api/v1/maintenance/branch-reset/execute` | `/maintenance` | destructive | Expiry/reuse/typed-confirmation/recompute/audit/no-secret tests | Retained with safety redesign |
| Navigate DB Downloader | `MainWindow.xaml` / `dbdownloader` item | `GET /api/v1/downloads/settings` | `/downloads` | read | Route, secret omission, no UNC/SMB detail contract | Retained |
| Save downloader settings | `DbDownloaderViewModel.SaveSettingsAsync` | `PUT /api/v1/downloads/settings` | `/downloads` | mutating | Endpoint/SMB policy, secret write-only semantics, validation | Retained with server ownership |
| Add/remove branch catalog entry | `DbDownloaderViewModel.AddBranchCodeAsync` / `RemoveBranchCode` | `POST /api/v1/downloads/branches`, `DELETE /api/v1/downloads/branches/{branchId}` | `/downloads` | mutating | Branch syntax, duplicate, authorization tests | Retained |
| Trigger backup batch | `DbDownloaderViewModel.TriggerJobAsync` / `DbDownloadService.RunAsync` | `POST /api/v1/downloads/batches` | `/downloads` | mutating | Single trigger, cancellation, independent branch progress, unsafe URL/SMB policy | Retained |
| Observe batch/branch state | `DbDownloaderViewModel.Jobs` | `GET /api/v1/operations/{operationId}`, `GET /api/v1/events` | `/downloads` | read | Refresh/SSE reconnect without replay, stale/offline state | Retained with Agent job registry |
| Download ready branch archive | `BranchBackupRowViewModel.DownloadAsync` / `DbDownloadService.DownloadAsync` | `GET /api/v1/artifacts/{artifactId}/content` | `/downloads` | read | Stream interruption, artifact authorization, no UNC/password response | Retained with opaque ID |
| Navigate / view activity | `MainWindow.xaml` / `log`; `LogHub` | `GET /api/v1/activity`, `GET /api/v1/events` | `/activity` | read | Redaction, cap/filter/correlation-ID test | Retained concept; sanitization added |
| Clear activity view | `LogViewModel.ClearLog` | none (client view-state clear) | `/activity` | mutating | UI-only clear does not delete audit evidence | Retained concept |
| Export diagnostics | none | `POST /api/v1/diagnostics/exports` | `/activity` | read | Secret scan and sanitized export tests | New Session 13 capability |
| Navigate overview | none | `GET /api/v1/overview` | `/` | read | Signal-path evidence, agent-unreachable state, responsive/a11y tests | New |

Every current NavigationView area—Configuration, Services, Operations, DB Downloader, and Log—and every command reachable from those areas is represented above. Infrastructure-only WinUI concepts (`DispatcherQueue` marshalling, `DispatcherQueueTimer`, process-wide elevation) have no feature parity row, as directed by plan section 8.6.1.
