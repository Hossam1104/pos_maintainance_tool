using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Application.Services;

public sealed class DbQueryService(IDatabaseService databaseService)
{
    public async Task<OperationResult> TestConnectionAsync(string clientName, ClientDbConfig config, CancellationToken cancellationToken = default)
    {
        var result = OperationResult.Running("test_client_db_connection");

        if (string.IsNullOrWhiteSpace(config.Server))
        {
            result.AddError("No server configured");
            result.Finalize(OperationStatus.Failed);
            return result;
        }

        try
        {
            await databaseService.TestConnectionAsync(new AppSettings(), config, cancellationToken).ConfigureAwait(false);
            result.AddMessage($"Connected to {clientName}");
            result.Finalize(OperationStatus.Success);
            return result;
        }
        catch (Exception ex)
        {
            result.AddError($"Connection failed: {ex.Message}");
            result.Finalize(OperationStatus.Failed);
            return result;
        }
    }

    public async Task<(OperationResult Result, IReadOnlyList<string> Codes)> GetRandomScannedCodesAsync(
        string clientName,
        ClientDbConfig config,
        int count,
        CancellationToken cancellationToken = default)
    {
        var result = OperationResult.Running("random_scanned_codes");

        if (count < 1)
        {
            result.AddError("Count must be greater than zero");
            result.Finalize(OperationStatus.Failed);
            return (result, []);
        }

        try
        {
            var codes = await databaseService.QueryRandomScannedCodesAsync(config, count, cancellationToken).ConfigureAwait(false);
            result.AddMessage($"Random query returned {codes.Count} code(s) from {clientName}/{config.Database}");
            result.Context["requested_count"] = count;
            result.Context["actual_count"] = codes.Count;
            result.Finalize(OperationStatus.Success);
            return (result, codes);
        }
        catch (Exception ex)
        {
            result.AddError($"Random query failed: {ex.Message}");
            result.Finalize(OperationStatus.Failed);
            return (result, []);
        }
    }
}
