using Dapper;
using Microsoft.Data.Sqlite;
using Pos.Data.Database;
using Pos.Domain;
using Pos.Domain.Security;
using Pos.Domain.Models;
using Pos.Domain.Time;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pos.Data.Services;

/// <summary>
/// Stub service for CSV import of products and customers.
/// When enabled, provides import preview functionality.
/// </summary>
public sealed class CsvImportService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ISessionStore _sessions;
    private readonly IClock _clock;

    public CsvImportService(IDbConnectionFactory factory, ISessionStore sessions, IClock clock)
    {
        _factory = factory;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var json = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT value_json FROM settings WHERE key = 'catalog.csv_import_enabled' AND scope = 'global' AND scope_id = '';",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Pos.Data.Services.SqlText.DisplaySetting(json) == "true";
    }

    public async Task<CsvImportPreview> PreviewImportAsync(
        string csvContent,
        string importType,
        CancellationToken cancellationToken = default)
    {
        RequireSession();

        // Stub: parse CSV header and return preview count
        var lines = csvContent.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
        var rowCount = lines.Length > 1 ? lines.Length - 1 : 0;

        return new CsvImportPreview(
            ImportType: importType,
            RowCount: rowCount,
            ColumnCount: lines.Length > 0 ? lines[0].Split(',').Length : 0,
            CanImport: rowCount > 0,
            Message: $"Preview: {rowCount} baris siap diimpor.");
    }

    public async Task<int> CommitImportAsync(
        string csvContent,
        string importType,
        CancellationToken cancellationToken = default)
    {
        RequireSession();

        // Stub: always returns 0 (not actually implemented)
        await Task.CompletedTask.ConfigureAwait(false);
        return 0;
    }

    private UserSession RequireSession() =>
        _sessions.Current ?? throw new PermissionException("Sesi tidak ditemukan. Silakan masuk kembali.");
}

/// <summary>CSV import preview result.</summary>
public sealed record CsvImportPreview(
    string ImportType,
    int RowCount,
    int ColumnCount,
    bool CanImport,
    string Message);
