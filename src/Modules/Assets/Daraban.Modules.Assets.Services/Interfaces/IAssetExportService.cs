using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services.Interfaces;

public interface IAssetExportService
{
    Task<Result<(Stream Stream, string ContentType, string FileName)>> ExportAsync(Guid entityNodeId, string format, string? status, Guid? assetTypeId, Guid? locationId, string? search, CancellationToken ct = default);
}
