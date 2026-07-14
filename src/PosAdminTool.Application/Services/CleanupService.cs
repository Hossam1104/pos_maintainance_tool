using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Application.Services;

public sealed class CleanupService(IDatabaseService databaseService, IServiceManager serviceManager)
{
    public async Task<OperationResult> CleanupFilesAsync(AppSettings settings, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = OperationResult.Running("cleanup_files");
        var deleted = 0;
        var skipped = 0;
        var failures = 0;

        foreach (var serviceName in settings.Services)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                progress?.Report($"Stopping {serviceName}...");
                await serviceManager.ControlAsync(serviceName, ServiceControlAction.Stop, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failures++;
                result.AddMessage($"Skipped service stop for {serviceName}: {ex.Message}");
            }
        }

        foreach (var rawFolder in settings.FoldersToDelete)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = Environment.ExpandEnvironmentVariables(rawFolder);
            progress?.Report($"Processing {folder}...");

            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                    deleted++;
                }
                else if (File.Exists(folder))
                {
                    File.Delete(folder);
                    deleted++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (Exception ex)
            {
                skipped++;
                failures++;
                result.AddMessage($"Skipped {folder}: {ex.Message}");
            }
        }

        result.AddMessage($"Cleanup completed. Deleted: {deleted}, Skipped: {skipped}");
        if (failures > 0 && deleted > 0)
        {
            result.Finalize(OperationStatus.PartialSuccess);
        }
        else if (failures > 0)
        {
            result.Finalize(OperationStatus.Failed);
        }
        else
        {
            result.Finalize(OperationStatus.Success);
        }
        return result;
    }

    public async Task<OperationResult> ResetBranchDataAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var result = OperationResult.Running("reset_branch_data");

        var branchCode = settings.BranchCode?.Trim();
        if (string.IsNullOrWhiteSpace(branchCode))
        {
            result.AddError("Branch code is invalid");
            result.Finalize(OperationStatus.Failed);
            return result;
        }

        try
        {
            await databaseService.ResetBranchDataAsync(settings, branchCode, cancellationToken).ConfigureAwait(false);
            result.AddMessage("Branch reset completed");
            result.Finalize(OperationStatus.Success);
            return result;
        }
        catch (Exception ex)
        {
            result.AddError($"Branch reset failed: {ex.Message}");
            result.Finalize(OperationStatus.Failed);
            return result;
        }
    }
}
