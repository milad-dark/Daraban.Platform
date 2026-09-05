using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Platform.Common;
using System.Globalization;

namespace Daraban.Modules.Assets.Services;

public class AssetImportService : IAssetImportService
{
    private readonly IAssetRepository _assetRepository;
    private readonly IAssetTypeRepository _assetTypeRepository;

    public AssetImportService(IAssetRepository assetRepository, IAssetTypeRepository assetTypeRepository)
    {
        _assetRepository = assetRepository;
        _assetTypeRepository = assetTypeRepository;
    }

    public async Task<Result<ImportResult>> ImportAsync(Stream fileStream, string fileName, Guid entityNodeId, Guid actorUserId, bool dryRun = false, CancellationToken ct = default)
    {
        var rows = await ParseFileAsync(fileStream, fileName, ct);
        if (rows is null)
            return Result.Failure<ImportResult>(new Error(
                "ASSETS.IMPORT_INVALID_FILE", "Could not parse file. Expected CSV or XLSX.", ErrorType.Validation));

        if (rows.Count == 0)
            return Result.Failure<ImportResult>(new Error(
                "ASSETS.IMPORT_EMPTY_FILE", "File contains no data rows.", ErrorType.Validation));

        // Pre-load asset types for name→id resolution
        var assetTypes = await _assetTypeRepository.GetAllAsync(ct);
        var assetTypeMap = assetTypes
            .Where(t => !t.DeletedAt.HasValue)
            .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

        var results = new List<ImportRowResult>();
        var successCount = 0;
        var errorCount = 0;
        var seenAssetTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSerialNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNum = i + 2; // +2: 1-indexed + header row
            var errors = ValidateRow(row, assetTypeMap);

            if (errors.Count > 0)
            {
                results.Add(new ImportRowResult(rowNum, false, row.Name, errors));
                errorCount++;
                continue;
            }

            if (!dryRun)
            {
                var now = DateTimeOffset.UtcNow;
                var asset = new Asset
                {
                    Id = Guid.CreateVersion7(),
                    EntityNodeId = entityNodeId,
                    AssetTypeId = assetTypeMap[row.AssetType].Id,
                    Name = row.Name,
                    AssetTag = row.AssetTag,
                    SerialNumber = row.SerialNumber,
                    Status = ParseStatus(row.Status),
                    PurchaseDate = ParseDateOnly(row.PurchaseDate),
                    PurchaseCost = ParseDecimal(row.PurchaseCost),
                    PurchaseCurrency = row.PurchaseCurrency,
                    OrderNumber = row.OrderNumber,
                    SupplierName = row.SupplierName,
                    WarrantyExpiry = ParseDateOnly(row.WarrantyExpiry),
                    Notes = row.Notes,
                    CreatedAt = now,
                    UpdatedAt = now,
                };

                // Check unique constraints (DB + in-memory for same-batch duplicates)
                if (!string.IsNullOrWhiteSpace(asset.AssetTag))
                {
                    var tagExists = await _assetRepository.AssetTagExistsAsync(asset.AssetTag, null, ct)
                                     || !seenAssetTags.Add(asset.AssetTag);
                    if (tagExists)
                    {
                        results.Add(new ImportRowResult(rowNum, false, row.Name, new[] { "AssetTag already exists." }));
                        errorCount++;
                        continue;
                    }
                }

                if (!string.IsNullOrWhiteSpace(asset.SerialNumber))
                {
                    var serialExists = await _assetRepository.SerialNumberExistsAsync(asset.SerialNumber, null, ct)
                                       || !seenSerialNumbers.Add(asset.SerialNumber);
                    if (serialExists)
                    {
                        results.Add(new ImportRowResult(rowNum, false, row.Name, new[] { "SerialNumber already exists." }));
                        errorCount++;
                        continue;
                    }
                }

                await _assetRepository.AddAsync(asset, ct);
            }

            results.Add(new ImportRowResult(rowNum, true, row.Name, Array.Empty<string>()));
            successCount++;
        }

        if (!dryRun && successCount > 0)
            await _assetRepository.SaveChangesAsync(ct);

        return Result.Success(new ImportResult(dryRun, rows.Count, successCount, errorCount, results));
    }

    public Stream GetTemplate()
    {
        var ms = new MemoryStream();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
        };
        using (var writer = new StreamWriter(ms, leaveOpen: true))
        using (var csv = new CsvWriter(writer, config))
        {
            csv.WriteField("Name");
            csv.WriteField("AssetType");
            csv.WriteField("AssetTag");
            csv.WriteField("SerialNumber");
            csv.WriteField("Status");
            csv.WriteField("PurchaseDate");
            csv.WriteField("PurchaseCost");
            csv.WriteField("PurchaseCurrency");
            csv.WriteField("WarrantyExpiry");
            csv.WriteField("OrderNumber");
            csv.WriteField("SupplierName");
            csv.WriteField("Notes");
            csv.NextRecord();
        }
        ms.Position = 0;
        return ms;
    }

    private async Task<List<ImportAssetRow>?> ParseFileAsync(Stream stream, string fileName, CancellationToken ct)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".csv" => await ParseCsvAsync(stream, ct),
            ".xlsx" => await ParseExcelAsync(stream, ct),
            _ => null
        };
    }

    private Task<List<ImportAssetRow>> ParseCsvAsync(Stream stream, CancellationToken ct)
    {
        var rows = new List<ImportAssetRow>();
        using var reader = new StreamReader(stream, leaveOpen: true);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
        };
        using var csv = new CsvReader(reader, config);

        // Read() + ReadHeader() must both be called before the record loop. Read() alone advances
        // onto the header line but does NOT register it as the header, so reading fields by name
        // would fail with ColumnCount: 0.
        if (!csv.Read())
            return Task.FromResult(rows); // completely empty file, not even a header

        csv.ReadHeader();

        // Fields are read by header name rather than via GetRecord<ImportAssetRow>() + a ClassMap.
        // ImportAssetRow is a positional record with no parameterless constructor, and CsvHelper's
        // ClassMap path requires one -- GetRecord() threw
        // MissingMethodException("Constructor 'ImportAssetRow()' was not found") on every single
        // upload. Reading by name also makes CSV parsing behave like ParseExcelAsync, which
        // already maps columns by header, so a reordered or partial column set works in both.
        while (csv.Read())
        {
            rows.Add(new ImportAssetRow(
                Name: Field(csv, "Name") ?? string.Empty,
                AssetType: Field(csv, "AssetType") ?? string.Empty,
                AssetTag: Field(csv, "AssetTag"),
                SerialNumber: Field(csv, "SerialNumber"),
                Status: Field(csv, "Status"),
                PurchaseDate: Field(csv, "PurchaseDate"),
                PurchaseCost: Field(csv, "PurchaseCost"),
                PurchaseCurrency: Field(csv, "PurchaseCurrency"),
                WarrantyExpiry: Field(csv, "WarrantyExpiry"),
                OrderNumber: Field(csv, "OrderNumber"),
                SupplierName: Field(csv, "SupplierName"),
                Notes: Field(csv, "Notes")));
        }

        return Task.FromResult(rows);
    }

    /// <summary>Reads one field by header name, returning null for an absent column or an empty
    /// cell so downstream "is this set?" checks work uniformly.</summary>
    private static string? Field(CsvReader csv, string header)
    {
        if (!csv.TryGetField<string>(header, out var value))
            return null;

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private Task<List<ImportAssetRow>> ParseExcelAsync(Stream stream, CancellationToken ct)
    {
        var rows = new List<ImportAssetRow>();

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var headerRow = worksheet.Row(1);
        var colCount = headerRow.CellsUsed().Count();

        // Map column headers to indices
        var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int c = 1; c <= colCount; c++)
        {
            var header = headerRow.Cell(c).GetString().Trim();
            if (!string.IsNullOrEmpty(header))
                colMap[header] = c;
        }

        int rowCount = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (int r = 2; r <= rowCount; r++)
        {
            var row = worksheet.Row(r);
            if (row.CellsUsed().All(c => string.IsNullOrWhiteSpace(c.GetString())))
                continue; // skip empty rows

            var record = new ImportAssetRow(
                Name: GetCell(row, colMap, "Name"),
                AssetType: GetCell(row, colMap, "AssetType"),
                AssetTag: GetCellOrNull(row, colMap, "AssetTag"),
                SerialNumber: GetCellOrNull(row, colMap, "SerialNumber"),
                Status: GetCellOrNull(row, colMap, "Status"),
                PurchaseDate: GetCellOrNull(row, colMap, "PurchaseDate"),
                PurchaseCost: GetCellOrNull(row, colMap, "PurchaseCost"),
                PurchaseCurrency: GetCellOrNull(row, colMap, "PurchaseCurrency"),
                WarrantyExpiry: GetCellOrNull(row, colMap, "WarrantyExpiry"),
                OrderNumber: GetCellOrNull(row, colMap, "OrderNumber"),
                SupplierName: GetCellOrNull(row, colMap, "SupplierName"),
                Notes: GetCellOrNull(row, colMap, "Notes"));

            rows.Add(record);
        }

        return Task.FromResult(rows);
    }

    private static string GetCell(IXLRow row, Dictionary<string, int> colMap, string header)
        => colMap.TryGetValue(header, out var col) ? row.Cell(col).GetString().Trim() : string.Empty;

    private static string? GetCellOrNull(IXLRow row, Dictionary<string, int> colMap, string header)
        => colMap.TryGetValue(header, out var col) ? row.Cell(col).GetString().Trim() : null;

    private static List<string> ValidateRow(ImportAssetRow row, Dictionary<string, AssetType> assetTypeMap)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(row.Name))
            errors.Add("Name is required.");

        if (string.IsNullOrWhiteSpace(row.AssetType))
            errors.Add("AssetType is required.");
        else if (!assetTypeMap.ContainsKey(row.AssetType))
            errors.Add($"AssetType '{row.AssetType}' does not exist.");

        if (!string.IsNullOrWhiteSpace(row.Status) && !Enum.TryParse<AssetStatus>(row.Status, true, out _))
            errors.Add($"Invalid Status '{row.Status}'. Valid values: InStock, InUse, UnderMaintenance, Archived, Retired, Disposed.");

        if (!string.IsNullOrWhiteSpace(row.PurchaseDate) && !DateOnly.TryParse(row.PurchaseDate, out _))
            errors.Add($"Invalid PurchaseDate '{row.PurchaseDate}'. Use YYYY-MM-DD format.");

        if (!string.IsNullOrWhiteSpace(row.PurchaseCost) && !decimal.TryParse(row.PurchaseCost, out _))
            errors.Add($"Invalid PurchaseCost '{row.PurchaseCost}'. Must be a number.");

        if (!string.IsNullOrWhiteSpace(row.WarrantyExpiry) && !DateOnly.TryParse(row.WarrantyExpiry, out _))
            errors.Add($"Invalid WarrantyExpiry '{row.WarrantyExpiry}'. Use YYYY-MM-DD format.");

        return errors;
    }

    private static AssetStatus ParseStatus(string? value)
        => Enum.TryParse<AssetStatus>(value, true, out var status) ? status : AssetStatus.InStock;

    private static DateOnly? ParseDateOnly(string? value)
        => DateOnly.TryParse(value, out var result) ? result : null;

    private static decimal? ParseDecimal(string? value)
        => decimal.TryParse(value, out var result) ? result : null;
}
