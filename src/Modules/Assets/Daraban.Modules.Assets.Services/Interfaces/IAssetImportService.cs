using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services.Interfaces;

public interface IAssetImportService
{
    Task<Result<ImportResult>> ImportAsync(Stream fileStream, string fileName, Guid entityNodeId, Guid actorUserId, bool dryRun = false, CancellationToken ct = default);

    Stream GetTemplate();
}
