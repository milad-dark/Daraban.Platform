using Daraban.Modules.Financial.Data.Entities;
using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Services.Interfaces;

public interface IBudgetService
{
    Task<Result<BudgetPagedResult>> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Result<BudgetDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<BudgetDto>> CreateAsync(CreateBudgetRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result<BudgetDto>> UpdateAsync(Guid id, UpdateBudgetRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
    Task<Result<BudgetSummaryDto>> GetSummaryAsync(Guid entityNodeId, CancellationToken ct = default);
}
