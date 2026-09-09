namespace Daraban.Modules.Reporting.Services.Reports;

/// <summary>
/// The neutral tabular model every report data provider emits and every renderer consumes
/// (Task 7.2). Adding a new dataset or a new export format never touches the other side.
/// All cell values are rendered-safe primitives; anything complex must be projected to
/// string/number/bool/DateTimeOffset in the provider.
/// </summary>
public sealed class ReportTable
{
    public required string DatasetKey { get; init; }

    public required IReadOnlyList<ReportColumn> Columns { get; init; }

    public required IReadOnlyList<IReadOnlyList<object?>> Rows { get; init; }

    /// <summary>One catalog column descriptor (key + header + kind).</summary>
    public sealed record ReportColumn(string Key, string Header, ReportCatalog.ReportColumnKind Kind);
}
