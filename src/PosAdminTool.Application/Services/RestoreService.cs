using System.IO.Compression;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Application.Services;

public sealed class RestoreService(IDatabaseService databaseService)
{
    public async Task<OperationResult> RestoreAsync(
        AppSettings settings,
        string backupZip,
        string? targetDatabase = null,
        string? dbFilesPath = null,
        string restoreType = "Full",
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = OperationResult.Running("restore_database");

        if (string.IsNullOrWhiteSpace(backupZip) || !File.Exists(backupZip))
        {
            result.AddError("Backup archive not found");
            result.Finalize(OperationStatus.Failed);
            return result;
        }

        var normalizedRestoreType = NormalizeRestoreType(restoreType);
        var restoreDatabase = !string.Equals(normalizedRestoreType, "config only", StringComparison.Ordinal);
        var restoreConfig = !string.Equals(normalizedRestoreType, "database only", StringComparison.Ordinal);

        var restoreDb = string.IsNullOrWhiteSpace(targetDatabase)
            ? DatabaseResolver.ResolveBranchDatabase(settings)
            : targetDatabase.Trim();

        var filesPath = string.IsNullOrWhiteSpace(dbFilesPath) ? settings.DbFilesPath : dbFilesPath.Trim();
        if (restoreDatabase)
        {
            Directory.CreateDirectory(filesPath);
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"pos_restore_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            progress?.Report("Extracting backup archive...");
            ZipFile.ExtractToDirectory(backupZip, tempDir);

            if (restoreConfig)
            {
                RestoreConfigFiles(settings, tempDir, result, progress);
            }

            if (!restoreDatabase)
            {
                result.AddMessage("Config restore completed");
                result.Finalize(OperationStatus.Success);
                return result;
            }

            var bakFile = ResolveBackupFile(tempDir, restoreDb);
            if (bakFile is null)
            {
                result.AddError("No .bak file found in archive");
                result.Finalize(OperationStatus.Failed);
                return result;
            }

            progress?.Report($"Reading SQL file list from {Path.GetFileName(bakFile)}...");
            var logicalFiles = await databaseService.ReadRestoreFileListAsync(settings, bakFile, cancellationToken).ConfigureAwait(false);
            if (logicalFiles.Count == 0)
            {
                logicalFiles =
                [
                    new RestoreFileInfo(restoreDb, "D"),
                    new RestoreFileInfo($"{restoreDb}_log", "L")
                ];
            }

            progress?.Report($"Restoring database {restoreDb}...");
            await databaseService.RestoreDatabaseAsync(settings, restoreDb, bakFile, logicalFiles, filesPath, cancellationToken).ConfigureAwait(false);

            result.AddMessage(restoreConfig
                ? "Full restore completed successfully"
                : "Database restore completed successfully");
            result.Context["target_database"] = restoreDb;
            result.Context["backup_file"] = bakFile;
            result.Finalize(OperationStatus.Success);
            return result;
        }
        catch (Exception ex)
        {
            result.AddError($"Restore failed: {ex.Message}");
            result.Finalize(OperationStatus.Failed);
            return result;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
                // Best effort temp cleanup.
            }
        }
    }

    private static string NormalizeRestoreType(string restoreType)
    {
        return string.Join(
            ' ',
            (restoreType ?? "Full").Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? ResolveBackupFile(string tempDir, string targetDatabase)
    {
        var bakFiles = Directory.EnumerateFiles(tempDir, "*.bak", SearchOption.TopDirectoryOnly).ToList();
        if (bakFiles.Count == 0)
        {
            return null;
        }

        var match = bakFiles.FirstOrDefault(path =>
            Path.GetFileNameWithoutExtension(path).Contains(targetDatabase, StringComparison.OrdinalIgnoreCase));
        return match ?? bakFiles[0];
    }

    private static void RestoreConfigFiles(
        AppSettings settings,
        string tempDir,
        OperationResult result,
        IProgress<string>? progress)
    {
        var restored = 0;
        foreach (var jsonFile in Directory.EnumerateFiles(tempDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(jsonFile);
            var destination = ResolveConfigDestination(settings, fileName);
            if (string.IsNullOrWhiteSpace(destination))
            {
                result.AddMessage($"Skipped unrecognized config file: {fileName}");
                continue;
            }

            var destinationDirectory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(jsonFile, destination, overwrite: true);
            restored++;
            progress?.Report($"Restored config file: {fileName}");
            result.AddMessage($"Restored config: {fileName} -> {destination}");
        }

        if (restored == 0)
        {
            result.AddMessage("No config files were found in the archive.");
        }
    }

    private static string? ResolveConfigDestination(AppSettings settings, string fileName)
    {
        if (fileName.Contains("branch", StringComparison.OrdinalIgnoreCase))
        {
            return settings.BranchConfigPath;
        }

        if (fileName.Contains("cashierserver", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("cashiergrpc", StringComparison.OrdinalIgnoreCase))
        {
            return settings.CashierGrpcConfigPath;
        }

        if (fileName.Contains("cashierui", StringComparison.OrdinalIgnoreCase))
        {
            return settings.CashierUiConfigPath;
        }

        return null;
    }
}
