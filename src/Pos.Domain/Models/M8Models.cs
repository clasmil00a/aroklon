using Pos.Domain.Models;

namespace Pos.Domain.Models;

/// <summary>Report definition.</summary>
public sealed record ReportDefinition(
    string Id,
    string Name,
    string Description,
    string Category,
    bool HasDateFilter,
    bool HasCustomerFilter,
    bool HasUserFilter,
    bool HasProductFilter,
    bool CanPrint,
    bool CanExport);

/// <summary>Report parameters.</summary>
public sealed record ReportParameters(
    string ReportId,
    DateTime? FromDate,
    DateTime? ToDate,
    string? CustomerId,
    string? UserId,
    string? ProductId,
    string GroupBy);

/// <summary>Report result.</summary>
public sealed record ReportResult(
    string ReportId,
    string ReportName,
    string GeneratedAt,
    int RecordCount,
    string Data,
    string Format);