using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services.Interfaces;

public interface IAssetExportService
{
    Task<Result<(Stream Stream, string ContentType, string FileName)>> ExportAsync(Guid entityNodeId, string format, string? status, Guid? assetTypeId, Guid? locationId, string? search, CancellationToken ct = default);

    /// <summary>
    /// Task 8.2: streams the CSV export directly into <paramref name="output"/> (rows are written
    /// as EF/Npgsql fetch them — no buffering of the full result set). XLSX cannot stream without a
    /// SAX-style writer and stays buffered via <see cref="ExportAsync"/>.
    /// </summary>
    Task WriteCsvAsync(Guid entityNodeId, Stream output, string? status, Guid? assetTypeId, Guid? locationId, string? search, CancellationToken ct = default);
}
