using Daraban.Platform.Common;

namespace Daraban.Modules.Reporting.Services.Reports;

/// <summary>
/// One implementation per catalog dataset (Task 7.2). Providers run AsNoTracking, entity-scoped
/// queries and project to the neutral <see cref="ReportTable"/>; they never know about export
/// formats. Expected failures return <see cref="Result{T}"/> -- no exceptions for control flow.
/// </summary>
public interface IReportDataProvider
{
    /// <summary>The ReportCatalog key this provider serves.</summary>
    string DatasetKey { get; }

    Task<Result<ReportTable>> GetTableAsync(
        Guid entityId,
        IReadOnlyDictionary<string, string> filters,
        IReadOnlyList<string> columns,
        CancellationToken ct = default);
}
