using Daraban.Modules.Knowledge.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Knowledge.Services.Interfaces;

public interface IKbCategoryService
{
    Task<Result<IReadOnlyList<KbCategoryDto>>> GetAllAsync(
        Guid entityNodeId, bool includeInactive, CancellationToken ct = default);

    Task<Result<IReadOnlyList<KbCategoryTreeDto>>> GetTreeAsync(
        Guid entityNodeId, bool includeInactive, CancellationToken ct = default);

    Task<Result<KbCategoryDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<KbCategoryDto>> CreateAsync(
        CreateKbCategoryRequest request, Guid entityNodeId, Guid actorUserId, CancellationToken ct = default);

    Task<Result<KbCategoryDto>> UpdateAsync(
        Guid id, UpdateKbCategoryRequest request, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Soft delete. Refused while the category still has child categories or articles --
    /// the caller must move or delete those first.</summary>
    Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
}
