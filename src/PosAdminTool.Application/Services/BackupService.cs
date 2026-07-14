using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Application.Services;

public sealed partial class BackupService(IDatabaseService databaseService)
{
    private static readonly IReadOnlyDictionary<string, string> DatabaseLabelMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["rmsbranchsrv database"] = "RmsBranchSrv",
        ["rmscashiersrv database"] = "RmsCashierSrv"
    };

    public async Task<OperationResult> BackupAsync(
        AppSettings settings,
        IReadOnlyCollection<string> selectedItems,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = OperationResult.Running("backup_database");
        var selectedDbNames = ResolveSelectedDatabases(settings, selectedItems);
        var selectedConfigSources = ResolveSelectedConfigFiles(settings, selectedItems);

        if (selectedDbNames.Count == 0 && selectedConfigSources.Count == 0)
        {
            result.AddError("No backup items selected");
            result.Finalize(OperationStatus.Failed);
            return result;
        }

        var availableConfigSources = new List<(string SourcePath, string ArchiveName)>();
        foreach (var (sourcePath, archiveName) in selectedConfigSources)
        {
            if (File.Exists(sourcePath))
            {
                availableConfigSources.Add((sourcePath, archiveName));
            }
            else
            {
                result.AddMessage($"Skipped missing file: {sourcePath}");
            }
        }

        if (selectedDbNames.Count == 0 && availableConfigSources.Count == 0)
        {
            result.AddError("No backup items available");
            result.Finalize(OperationStatus.Failed);
            return result;
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var baseDir = string.IsNullOrWhiteSpace(settings.BackupFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Backups")
            : settings.BackupFolder;

        Directory.CreateDirectory(baseDir);

        var zipFile = Path.Combine(
            baseDir,
            string.Concat(
                SafeFilenamePart(settings.ClientName, "Client"),
                "_",
                SafeFilenamePart(settings.BranchCode, "Branch"),
                "_POS_",
                SafeFilenamePart(settings.PosNumber, "00"),
                "_DB_Backup_",
                timestamp,
                ".zip"));

        var stagingDir = Path.Combine(Path.GetTempPath(), $"pos_backup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDir);
        var tempSqlBackups = new List<string>();

        try
        {
            var stagedArtifacts = new List<string>();

            foreach (var dbName in selectedDbNames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var backupFile = Path.Combine(baseDir, $"{SafeFilenamePart(dbName, "database")}_{timestamp}.bak");
                progress?.Report($"Creating SQL backup for {dbName}...");

                try
                {
                    await databaseService.BackupDatabaseAsync(settings, dbName, backupFile, useCompatibilityMode: false, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    progress?.Report($"Retrying backup for {dbName} with compatibility mode...");
                    await databaseService.BackupDatabaseAsync(settings, dbName, backupFile, useCompatibilityMode: true, cancellationToken).ConfigureAwait(false);
                }

                if (!File.Exists(backupFile))
                {
                    result.AddError($"Backup file for {dbName} was not created");
                    result.Finalize(OperationStatus.Failed);
                    return result;
                }

                var stagedBackup = Path.Combine(stagingDir, Path.GetFileName(backupFile));
                File.Copy(backupFile, stagedBackup, overwrite: true);
                stagedArtifacts.Add(stagedBackup);
                tempSqlBackups.Add(backupFile);
            }

            foreach (var (sourcePath, archiveName) in availableConfigSources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Copying {Path.GetFileName(sourcePath)}...");
                var stagedFile = Path.Combine(stagingDir, SafeArchiveName(archiveName, Path.GetFileName(sourcePath)));
                File.Copy(sourcePath, stagedFile, overwrite: true);
                stagedArtifacts.Add(stagedFile);
            }

            progress?.Report($"Staged {stagedArtifacts.Count} item(s)");
            progress?.Report("Compressing selected backup items...");

            if (File.Exists(zipFile))
            {
                File.Delete(zipFile);
            }

            using (var archive = ZipFile.Open(zipFile, ZipArchiveMode.Create))
            {
                foreach (var item in stagedArtifacts)
                {
                    archive.CreateEntryFromFile(item, Path.GetFileName(item), CompressionLevel.Optimal);
                }
            }

            TryOpenFolder(baseDir, progress);

            result.AddMessage($"Backup completed: {zipFile}");
            result.AddMessage($"Databases: {selectedDbNames.Count}, Files: {availableConfigSources.Count}, Output: {zipFile}");
            result.Context["zip_file"] = zipFile;
            result.Finalize(OperationStatus.Success);
            return result;
        }
        catch (Exception ex)
        {
            result.AddError($"Backup failed: {ex.Message}");
            result.Finalize(OperationStatus.Failed);
            return result;
        }
        finally
        {
            foreach (var backupFile in tempSqlBackups.Where(File.Exists))
            {
                try
                {
                    File.Delete(backupFile);
                }
                catch
                {
                    // Best effort cleanup of intermediate SQL backups.
                }
            }

            try
            {
                if (Directory.Exists(stagingDir))
                {
                    Directory.Delete(stagingDir, recursive: true);
                }
            }
            catch
            {
                // Best effort temp cleanup.
            }
        }
    }

    private static List<string> ResolveSelectedDatabases(AppSettings settings, IEnumerable<string> selectedItems)
    {
        var databases = new List<string>();

        foreach (var item in selectedItems)
        {
            var normalized = NormalizeLabel(item);
            if (!DatabaseLabelMap.TryGetValue(normalized, out var defaultDb))
            {
                continue;
            }

            var resolved = ResolveDbNameFromLabel(settings, normalized, defaultDb);
            if (!databases.Contains(resolved, StringComparer.OrdinalIgnoreCase))
            {
                databases.Add(resolved);
            }
        }

        return databases;
    }

    private static List<(string SourcePath, string ArchiveName)> ResolveSelectedConfigFiles(AppSettings settings, IEnumerable<string> selectedItems)
    {
        var fileLabelMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rms_branchservice_appsettings.json"] = settings.BranchConfigPath,
            ["rms_cashierserver_appsettings.json"] = settings.CashierGrpcConfigPath,
            ["rms_cashierui_appsettings.json"] = settings.CashierUiConfigPath
        };

        var files = new List<(string SourcePath, string ArchiveName)>();
        foreach (var item in selectedItems)
        {
            var normalized = NormalizeLabel(item);
            if (fileLabelMap.TryGetValue(normalized, out var path) && !string.IsNullOrWhiteSpace(path))
            {
                files.Add((path, SafeArchiveName(item.Trim(), Path.GetFileName(path))));
            }
        }

        return files;
    }

    private static string ResolveDbNameFromLabel(AppSettings settings, string normalizedItem, string defaultDb)
    {
        if (settings.Databases.Count == 0)
        {
            return defaultDb;
        }

        if (normalizedItem.Contains("branch", StringComparison.OrdinalIgnoreCase))
        {
            var branch = settings.Databases.FirstOrDefault(db => db.Contains("branch", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(branch))
            {
                return branch;
            }
        }

        if (normalizedItem.Contains("cashier", StringComparison.OrdinalIgnoreCase))
        {
            var cashier = settings.Databases.FirstOrDefault(db => db.Contains("cashier", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(cashier))
            {
                return cashier;
            }
        }

        return settings.Databases.FirstOrDefault(db => string.Equals(db, defaultDb, StringComparison.OrdinalIgnoreCase)) ?? defaultDb;
    }

    private static string NormalizeLabel(string text)
    {
        return string.Join(' ', (text ?? string.Empty).Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string SafeFilenamePart(string? text, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
        candidate = UnsafeFilenamePartRegex().Replace(candidate, "_");
        candidate = RepeatedUnderscoreRegex().Replace(candidate, "_").Trim('_');
        return string.IsNullOrWhiteSpace(candidate) ? fallback : candidate;
    }

    private static string SafeArchiveName(string? text, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
        candidate = UnsafeArchiveNameRegex().Replace(candidate, "_").Replace("..", "_", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(candidate) ? fallback : candidate;
    }

    private static void TryOpenFolder(string path, IProgress<string>? progress)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
        }
        catch
        {
            progress?.Report($"Backup folder: {path}");
        }
    }

    [GeneratedRegex("[^A-Za-z0-9_-]+")]
    private static partial Regex UnsafeFilenamePartRegex();

    [GeneratedRegex("_+")]
    private static partial Regex RepeatedUnderscoreRegex();

    [GeneratedRegex("[<>:\"/\\\\|?*]+")]
    private static partial Regex UnsafeArchiveNameRegex();
}
