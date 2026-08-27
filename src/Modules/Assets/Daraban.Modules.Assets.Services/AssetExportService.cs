using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Platform.Common;
using System.Globalization;
using System.Text;

namespace Daraban.Modules.Assets.Services;

public class AssetExportService : IAssetExportService
{
    private readonly IAssetRepository _assetRepository;

    public AssetExportService(IAssetRepository assetRepository)
    {
        _assetRepository = assetRepository;
    }

    public async Task<Result<(Stream Stream, string ContentType, string FileName)>> ExportAsync(Guid entityNodeId, string format, string? status, Guid? assetTypeId, Guid? locationId, string? search, CancellationToken ct = default)
    {
        AssetStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AssetStatus>(status, true, out var s))
            parsedStatus = s;

        // Fetch all matching assets (use large page size to get everything)
        var (items, _) = await _assetRepository.GetPagedAsync(
            entityNodeId, parsedStatus, assetTypeId, locationId, search,
            page: 1, pageSize: int.MaxValue, ct);

        if (format.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
            return Result.Success((ExportToExcel(items), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "assets.xlsx"));

        return Result.Success((ExportToCsv(items), "text/csv", "assets.csv"));
    }

    private static Stream ExportToCsv(IReadOnlyList<Asset> assets)
    {
        var ms = new MemoryStream();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
        };
        using (var writer = new StreamWriter(ms, leaveOpen: true, encoding: new UTF8Encoding(false)))
        using (var csv = new CsvWriter(writer, config))
        {
            csv.Context.RegisterClassMap<AssetExportMap>();
            csv.WriteRecords(assets);
        }
        ms.Position = 0;
        return ms;
    }

    private static Stream ExportToExcel(IReadOnlyList<Asset> assets)
    {
        var ms = new MemoryStream();
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Assets");

        // Header row
        var headers = new[]
        {
            "Id", "Name", "AssetTag", "SerialNumber", "Status", "AssetType",
            "PurchaseDate", "PurchaseCost", "PurchaseCurrency",
            "WarrantyExpiry", "OrderNumber", "SupplierName", "Notes",
            "CreatedAt", "UpdatedAt"
        };
        for (int c = 0; c < headers.Length; c++)
            worksheet.Cell(1, c + 1).Value = headers[c];

        // Style header row
        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.CornflowerBlue;
        headerRange.Style.Font.FontColor = XLColor.White;

        // Data rows
        for (int r = 0; r < assets.Count; r++)
        {
            var a = assets[r];
            worksheet.Cell(r + 2, 1).Value = a.Id.ToString();
            worksheet.Cell(r + 2, 2).Value = a.Name;
            worksheet.Cell(r + 2, 3).Value = a.AssetTag ?? string.Empty;
            worksheet.Cell(r + 2, 4).Value = a.SerialNumber ?? string.Empty;
            worksheet.Cell(r + 2, 5).Value = a.Status.ToString();
            worksheet.Cell(r + 2, 6).Value = a.AssetType?.Name ?? string.Empty;
            worksheet.Cell(r + 2, 7).Value = a.PurchaseDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            worksheet.Cell(r + 2, 8).Value = a.PurchaseCost?.ToString() ?? string.Empty;
            worksheet.Cell(r + 2, 9).Value = a.PurchaseCurrency ?? string.Empty;
            worksheet.Cell(r + 2, 10).Value = a.WarrantyExpiry?.ToString("yyyy-MM-dd") ?? string.Empty;
            worksheet.Cell(r + 2, 11).Value = a.OrderNumber ?? string.Empty;
            worksheet.Cell(r + 2, 12).Value = a.SupplierName ?? string.Empty;
            worksheet.Cell(r + 2, 13).Value = a.Notes ?? string.Empty;
            worksheet.Cell(r + 2, 14).Value = a.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ");
            worksheet.Cell(r + 2, 15).Value = a.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private sealed class AssetExportMap : ClassMap<Asset>
    {
        public AssetExportMap()
        {
            Map(m => m.Id).Name("Id").TypeConverterOption.Format("D");
            Map(m => m.Name).Name("Name");
            Map(m => m.AssetTag).Name("AssetTag");
            Map(m => m.SerialNumber).Name("SerialNumber");
            Map(m => m.Status).Name("Status").TypeConverter<AssetStatusConverter>();
            Map(m => m.AssetType).Name("AssetType").TypeConverter<AssetTypeConverter>();
            Map(m => m.PurchaseDate).Name("PurchaseDate").TypeConverterOption.Format("yyyy-MM-dd");
            Map(m => m.PurchaseCost).Name("PurchaseCost");
            Map(m => m.PurchaseCurrency).Name("PurchaseCurrency");
            Map(m => m.WarrantyExpiry).Name("WarrantyExpiry").TypeConverterOption.Format("yyyy-MM-dd");
            Map(m => m.OrderNumber).Name("OrderNumber");
            Map(m => m.SupplierName).Name("SupplierName");
            Map(m => m.Notes).Name("Notes");
            Map(m => m.CreatedAt).Name("CreatedAt").TypeConverterOption.Format("yyyy-MM-ddTHH:mm:ssZ");
            Map(m => m.UpdatedAt).Name("UpdatedAt").TypeConverterOption.Format("yyyy-MM-ddTHH:mm:ssZ");
        }
    }

    private sealed class AssetStatusConverter : ITypeConverter
    {
        public string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
            => value is AssetStatus status ? status.ToString() : null;

        public object? ConvertFromString(string? text, IReaderRow row, MemberMapData mapData)
            => Enum.TryParse<AssetStatus>(text, true, out var status) ? status : AssetStatus.InStock;
    }

    private sealed class AssetTypeConverter : ITypeConverter
    {
        public string? ConvertToString(object? value, IWriterRow row, MemberMapData mapData)
            => value is AssetType assetType ? assetType.Name : null;

        public object? ConvertFromString(string? text, IReaderRow row, MemberMapData mapData)
            => throw new NotSupportedException("Import does not support AssetType navigation mapping.");
    }
}
