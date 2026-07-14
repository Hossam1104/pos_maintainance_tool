using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Infrastructure.Windows;

public sealed partial class SqlCmdExecutor : IDatabaseService
{
    public async Task TestConnectionAsync(AppSettings settings, ClientDbConfig? overrideConnection = null, CancellationToken cancellationToken = default)
    {
        var connectionString = overrideConnection is null
            ? BuildConnectionString(settings)
            : BuildClientConnectionString(overrideConnection);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand("SELECT 1;", connection) { CommandTimeout = 5 };
        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> BranchExistsAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SET NOCOUNT ON;
            SELECT CAST(CASE WHEN EXISTS
            (
                SELECT 1
                FROM Branches
                WHERE BranchCode = @branch_code
            )
            THEN 1 ELSE 0 END AS bit);
            """;

        await using var connection = new SqlConnection(BuildConnectionString(settings));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@branch_code", SqlDbType.NVarChar, 50).Value = branchCode;
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return ReadBoolScalar(value);
    }

    public async Task ResetBranchDataAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SET NOCOUNT ON;
            BEGIN TRY
                BEGIN TRAN;

                DELETE FROM Sales WHERE BranchCode = @branch_code;
                DELETE FROM CashierSessions WHERE BranchCode = @branch_code;
                DELETE FROM InventoryMovements WHERE BranchCode = @branch_code;

                COMMIT TRAN;
            END TRY
            BEGIN CATCH
                IF @@TRANCOUNT > 0 ROLLBACK TRAN;
                THROW;
            END CATCH;
            """;

        await using var connection = new SqlConnection(BuildConnectionString(settings));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        command.Parameters.Add("@branch_code", SqlDbType.NVarChar, 50).Value = branchCode;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> QueryRandomScannedCodesAsync(ClientDbConfig config, int count, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SET NOCOUNT ON;
            SELECT TOP (@count) ScannedCode
            FROM InvoiceItems
            WHERE SerialNumber IS NOT NULL
            ORDER BY NEWID();
            """;

        await using var connection = new SqlConnection(BuildClientConnectionString(config));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 };
        command.Parameters.Add("@count", SqlDbType.Int).Value = count;

        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false))
            {
                var code = reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(code))
                {
                    results.Add(code.Trim());
                }
            }
        }

        return results;
    }

    public async Task BackupDatabaseAsync(AppSettings settings, string databaseName, string backupFilePath, bool useCompatibilityMode, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath)!);

        var quotedDatabase = QuoteDatabaseName(databaseName);
        var options = useCompatibilityMode ? "WITH INIT;" : "WITH INIT, COMPRESSION, CHECKSUM;";
        var sql = string.Concat("BACKUP DATABASE ", quotedDatabase, " TO DISK = @backup_path ", options);

        await using var connection = new SqlConnection(BuildConnectionString(settings, "master"));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
        command.Parameters.Add("@backup_path", SqlDbType.NVarChar, 4000).Value = backupFilePath;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RestoreFileInfo>> ReadRestoreFileListAsync(AppSettings settings, string backupFilePath, CancellationToken cancellationToken = default)
    {
        const string sql = "RESTORE FILELISTONLY FROM DISK = @backup_path;";

        await using var connection = new SqlConnection(BuildConnectionString(settings, "master"));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        command.Parameters.Add("@backup_path", SqlDbType.NVarChar, 4000).Value = backupFilePath;

        var files = new List<RestoreFileInfo>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var logicalNameOrdinal = reader.GetOrdinal("LogicalName");
        var typeOrdinal = reader.GetOrdinal("Type");

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var logicalName = reader.GetString(logicalNameOrdinal);
            var fileType = reader.GetString(typeOrdinal);
            if (fileType is "D" or "L")
            {
                files.Add(new RestoreFileInfo(logicalName, fileType));
            }
        }

        return files;
    }

    public async Task RestoreDatabaseAsync(
        AppSettings settings,
        string targetDatabase,
        string backupFilePath,
        IReadOnlyList<RestoreFileInfo> logicalFiles,
        string dbFilesPath,
        CancellationToken cancellationToken = default)
    {
        var quotedDatabase = QuoteDatabaseName(targetDatabase);
        var moveClauses = BuildMoveClauses(targetDatabase, logicalFiles, dbFilesPath);

        var sql = string.Concat(
            "BEGIN TRY\n",
            "    IF DB_ID(@target_database) IS NOT NULL ALTER DATABASE ", quotedDatabase, " SET SINGLE_USER WITH ROLLBACK IMMEDIATE;\n",
            "    RESTORE DATABASE ", quotedDatabase, " FROM DISK = @backup_path WITH REPLACE, RECOVERY",
            moveClauses.Length > 0 ? string.Concat(", ", moveClauses) : string.Empty,
            ";\n",
            "    ALTER DATABASE ", quotedDatabase, " SET MULTI_USER;\n",
            "END TRY\n",
            "BEGIN CATCH\n",
            "    IF DB_ID(@target_database) IS NOT NULL ALTER DATABASE ", quotedDatabase, " SET MULTI_USER;\n",
            "    THROW;\n",
            "END CATCH;");

        await using var connection = new SqlConnection(BuildConnectionString(settings, "master"));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
        command.Parameters.Add("@target_database", SqlDbType.NVarChar, 128).Value = targetDatabase;
        command.Parameters.Add("@backup_path", SqlDbType.NVarChar, 4000).Value = backupFilePath;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private static string BuildConnectionString(AppSettings settings, string? database = null)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = string.IsNullOrWhiteSpace(settings.SqlInstance) ? "." : settings.SqlInstance,
            InitialCatalog = string.IsNullOrWhiteSpace(database)
                ? DatabaseResolver.ResolvePrimaryDatabase(settings)
                : database,
            TrustServerCertificate = true,
            ConnectTimeout = 5
        };

        if (string.IsNullOrWhiteSpace(settings.SqlUser))
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = settings.SqlUser;
            builder.Password = settings.SqlPassword;
        }

        return builder.ConnectionString;
    }

    private static string BuildClientConnectionString(ClientDbConfig config)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = config.Server,
            InitialCatalog = string.IsNullOrWhiteSpace(config.Database) ? "master" : config.Database,
            TrustServerCertificate = true,
            ConnectTimeout = 5
        };

        if (string.IsNullOrWhiteSpace(config.User))
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = config.User;
            builder.Password = config.Password;
        }

        return builder.ConnectionString;
    }

    private static string QuoteDatabaseName(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName) || !SafeIdentifierRegex().IsMatch(databaseName))
        {
            throw new InvalidOperationException("Database name contains unsupported characters.");
        }

        using var builder = new SqlCommandBuilder();
        return builder.QuoteIdentifier(databaseName);
    }

    private static string BuildMoveClauses(string targetDatabase, IReadOnlyList<RestoreFileInfo> logicalFiles, string dbFilesPath)
    {
        var clauses = new List<string>();
        foreach (var file in logicalFiles)
        {
            var extension = file.FileType == "L" ? ".ldf" : ".mdf";
            var suffix = file.FileType == "L" ? "_log" : string.Empty;
            var physicalPath = Path.Combine(dbFilesPath, string.Concat(targetDatabase, suffix, extension));

            clauses.Add(string.Concat(
                "MOVE N'",
                EscapeSqlLiteral(file.LogicalName),
                "' TO N'",
                EscapeSqlLiteral(physicalPath),
                "'"));
        }

        return string.Join(", ", clauses);
    }

    private static string EscapeSqlLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static bool ReadBoolScalar(object? value)
    {
        return value switch
        {
            bool boolean => boolean,
            byte or sbyte or short or int or long => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) != 0,
            _ => false
        };
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex SafeIdentifierRegex();
}
