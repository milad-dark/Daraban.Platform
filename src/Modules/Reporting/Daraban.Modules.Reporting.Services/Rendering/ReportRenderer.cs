using Daraban.Modules.Reporting.Services.Reports;

namespace Daraban.Modules.Reporting.Services.Rendering;

/// <summary>Well-known export format keys (Task 7.2).</summary>
public static class ReportFormats
{
    public const string Csv = "csv";
    public const string Xlsx = "xlsx";
    public const string Pdf = "pdf";

    public static readonly IReadOnlyList<string> All = [Csv, Xlsx, Pdf];

    public static bool IsSupported(string? format) =>
        format is not null && All.Contains(format.Trim().ToLowerInvariant());

    public static string ContentType(string format) => format.ToLowerInvariant() switch
    {
        Csv => "text/csv",
        Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        Pdf => "application/pdf",
        _ => "application/octet-stream",
    };

    public static string FileExtension(string format) => format.ToLowerInvariant() switch
    {
        Csv => "csv",
        Xlsx => "xlsx",
        Pdf => "pdf",
        _ => "bin",
    };
}

/// <summary>
/// One implementation per export format (Task 7.2). Renderers are pure: table in, bytes out --
/// no I/O, no DI dependencies beyond the renderer itself. CSV is always available; Excel uses
/// ClosedXML (MIT) and PDF uses QuestPDF (MIT), both already license-compatible.
/// </summary>
public interface IReportRenderer
{
    /// <summary>The ReportFormats key this renderer produces.</summary>
    string Format { get; }

    /// <summary>Renders the table to a fully-buffered byte array (these reports are bounded --
    /// catalog datasets are paged/filterable; reports large enough to not fit in memory would
    /// need a streaming pipeline first).</summary>
    byte[] Render(ReportTable table);
}
