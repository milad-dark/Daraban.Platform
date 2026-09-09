using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Daraban.Modules.Reporting.Services.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Daraban.Modules.Reporting.Services.Rendering;

// ---- Renderers (Task 7.2) ----------------------------------------------------------
// Pure table -> bytes converters. Header values come from the closed ReportCatalog, so no
// user-controlled string becomes a formula or markup injection target: CSV cells that begin
// with =,+,-,@ are prefixed with a single quote (OWASP CSV injection), Excel cells are set as
// text via SetValue with typed values, and PDF text is drawn as PDF primitives (never HTML).

/// <summary>CSV renderer (always available).</summary>
public sealed class CsvReportRenderer : IReportRenderer
{
    public string Format => ReportFormats.Csv;

    public byte[] Render(ReportTable table)
    {
        using var ms = new MemoryStream();
        using (var writer = new StreamWriter(ms, new UTF8Encoding(false), leaveOpen: true))
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
        }))
        {
            foreach (var column in table.Columns)
                csv.WriteField(CsvSanitize(column.Header));
            csv.NextRecord();

            foreach (var row in table.Rows)
            {
                foreach (var cell in row)
                    csv.WriteField(CsvSanitize(ReportCellFormatter.FormatCell(cell)));
                csv.NextRecord();
            }
        }

        return ms.ToArray();
    }

    /// <summary>Neutralizes spreadsheet formula injection: anything Excel/LibreOffice would
    /// evaluate gets a leading quote.</summary>
    private static string CsvSanitize(string value) =>
        value.Length > 0 && (value[0] is '=' or '+' or '-' or '@') ? $"'{value}" : value;
}

/// <summary>Excel renderer -- ClosedXML (MIT). The platform's existing Excel engine
/// (Assets import/export); EPPlus was evaluated and rejected: Polyform Noncommercial
/// license is not usable for a general-purpose platform backend.</summary>
public sealed class ExcelReportRenderer : IReportRenderer
{
    public string Format => ReportFormats.Xlsx;

    public byte[] Render(ReportTable table)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName(table.DatasetKey));

        for (var c = 0; c < table.Columns.Count; c++)
            sheet.Cell(1, c + 1).Value = table.Columns[c].Header;

        var headerRange = sheet.Range(1, 1, 1, Math.Max(table.Columns.Count, 1));
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.CornflowerBlue;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.FontSize = 11;

        for (var r = 0; r < table.Rows.Count; r++)
        {
            for (var c = 0; c < table.Columns.Count; c++)
                sheet.Cell(r + 2, c + 1).SetValue(ToXl(table.Rows[r][c]));
        }

        sheet.Columns().AdjustToContents(1, Math.Min(table.Rows.Count + 1, 200));

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static string SheetName(string datasetKey)
    {
        // Excel sheet names: max 31 chars, no : \ / ? * [ ].
        var name = new string(datasetKey.Take(31).ToArray());
        foreach (var bad in new[] { ':', '\\', '/', '?', '*', '[', ']' })
            name = name.Replace(bad, '_');
        return string.IsNullOrEmpty(name) ? "Report" : name;
    }

    private static XLCellValue ToXl(object? value) => value switch
    {
        null => Blank.Value,
        bool b => b,
        sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal
            => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTime dt => dt,
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Guid or string => value.ToString() ?? string.Empty,
        _ => value.ToString() ?? string.Empty,
    };
}

/// <summary>PDF renderer -- QuestPDF (MIT, license accepted via QuestPDF.Settings.License;
/// checked once per process in the module's composition root).</summary>
public sealed class PdfReportRenderer : IReportRenderer
{
    static PdfReportRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false;
    }

    public string Format => ReportFormats.Pdf;

    public byte[] Render(ReportTable table)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text(table.DatasetKey).FontSize(16).SemiBold();
                    col.Item().PaddingTop(2).Text($"Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingTop(8).Table(t =>
                {
                    t.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in table.Columns)
                            columns.RelativeColumn();
                    });

                    t.Header(header =>
                    {
                        foreach (var column in table.Columns)
                        {
                            header.Cell().Background(Colors.Indigo.Lighten2)
                                .Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                                .Padding(4).Text(column.Header).SemiBold();
                        }
                    });

                    foreach (var row in table.Rows)
                    {
                        foreach (var cell in row)
                        {
                            t.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(4).Text(ReportCellFormatter.FormatCell(cell));
                        }
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}

/// <summary>Shared primitive formatting for textual targets (CSV cells, PDF cells).</summary>
internal static class ReportCellFormatter
{
    public static string FormatCell(object? value) => value switch
    {
        null => string.Empty,
        bool b => b ? "true" : "false",
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
