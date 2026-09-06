using Daraban.Modules.Financial.Data.Entities;
using Daraban.Modules.Financial.Data.Repositories;
using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Modules.Financial.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Services;

public class BudgetService : IBudgetService
{
    private readonly IBudgetRepository _budgetRepository;

    public BudgetService(IBudgetRepository budgetRepository)
    {
        _budgetRepository = budgetRepository;
    }

    public async Task<Result<BudgetPagedResult>> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);

        var (items, totalCount) = await _budgetRepository.GetPagedAsync(
            entityNodeId, search, isActive, normalizedPage, normalizedPageSize, ct);

        var dtos = items.Select(MapToListDto).ToList();
        return Result.Success(new BudgetPagedResult(dtos, totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<BudgetDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var budget = await _budgetRepository.GetByIdWithDetailsAsync(id, ct);
        if (budget is null)
            return Result.Failure<BudgetDto>(new Error("BUDGET.NOT_FOUND", "Budget not found.", ErrorType.NotFound));

        return Result<BudgetDto>.Success(MapToDto(budget));
    }

    public async Task<Result<BudgetDto>> CreateAsync(CreateBudgetRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        // Validate unique name
        var nameExists = await _budgetRepository.NameExistsAsync(request.Name, request.EntityNodeId, null, ct);
        if (nameExists)
            return Result.Failure<BudgetDto>(BudgetNameExists(request.Name));

        // A budget that ends before it starts is unusable and shows negative remaining from day one.
        if (request.EndDate < request.StartDate)
            return Result.Failure<BudgetDto>(new Error(
                "BUDGET.INVALID_RANGE", "Budget end date must be on or after its start date.", ErrorType.Validation));

        if (request.Amount < 0)
            return Result.Failure<BudgetDto>(new Error(
                "BUDGET.NEGATIVE_AMOUNT", "Budget amount cannot be negative.", ErrorType.Validation));

        var budget = new Budget
        {
            Id = Guid.CreateVersion7(),
            EntityId = request.EntityNodeId,
            Name = request.Name,
            Reference = request.Reference,
            Amount = request.Amount,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            LocationId = request.LocationId,
            Comment = request.Comment,
            IsActive = true,
            ParentBudgetId = request.ParentBudgetId,
            CreatedById = actorUserId,
            UpdatedById = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _budgetRepository.AddAsync(budget, ct);
        await _budgetRepository.SaveChangesAsync(ct);

        return Result.Success(MapToDto(budget));
    }

    public async Task<Result<BudgetDto>> UpdateAsync(Guid id, UpdateBudgetRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var budget = await _budgetRepository.GetByIdAsync(id, ct);
        if (budget is null)
            return Result.Failure<BudgetDto>(BudgetNotFound());

        // Validate unique name (excluding current budget)
        var nameExists = await _budgetRepository.NameExistsAsync(request.Name, budget.EntityId, id, ct);
        if (nameExists)
            return Result.Failure<BudgetDto>(BudgetNameExists(request.Name));

        if (request.EndDate < request.StartDate)
            return Result.Failure<BudgetDto>(new Error(
                "BUDGET.INVALID_RANGE", "Budget end date must be on or after its start date.", ErrorType.Validation));

        if (request.Amount < 0)
            return Result.Failure<BudgetDto>(new Error(
                "BUDGET.NEGATIVE_AMOUNT", "Budget amount cannot be negative.", ErrorType.Validation));

        // A budget that has already been spent against must never have its ceiling dropped below
        // the amount actually spent -- that would retroactively make the overspend invisible.
        if (request.Amount < budget.Spent)
            return Result.Failure<BudgetDto>(new Error(
                "BUDGET.AMOUNT_BELOW_SPENT",
                $"Budget amount cannot be reduced below the {budget.Spent} already spent.",
                ErrorType.BusinessRule));

        budget.Name = request.Name;
        budget.Reference = request.Reference;
        budget.Amount = request.Amount;
        budget.StartDate = request.StartDate;
        budget.EndDate = request.EndDate;
        budget.LocationId = request.LocationId;
        budget.Comment = request.Comment;
        budget.IsActive = request.IsActive;
        budget.ParentBudgetId = request.ParentBudgetId;
        budget.UpdatedAt = DateTimeOffset.UtcNow;
        budget.UpdatedById = actorUserId;

        await _budgetRepository.UpdateAsync(budget, ct);
        await _budgetRepository.SaveChangesAsync(ct);

        return Result.Success(MapToDto(budget));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var budget = await _budgetRepository.GetByIdWithDetailsAsync(id, ct);
        if (budget is null)
            return Result.Failure(BudgetNotFound());

        // A budget with children, purchases, or infocom entries has live bookkeeping attached.
        // Orphaning it via a bare soft delete would leave those records pointing at nothing.
        if (budget.ChildBudgets.Count > 0)
            return Result.Failure(new Error(
                "BUDGET.HAS_CHILDREN", "Cannot delete a budget that has child budgets.", ErrorType.BusinessRule));

        if (budget.Purchases.Count > 0)
            return Result.Failure(new Error(
                "BUDGET.HAS_PURCHASES", "Cannot delete a budget that has purchases attached.", ErrorType.BusinessRule));

        if (budget.InfocomEntries.Count > 0)
            return Result.Failure(new Error(
                "BUDGET.HAS_INFOCOMS", "Cannot delete a budget that has infocom entries attached.", ErrorType.BusinessRule));

        var now = DateTimeOffset.UtcNow;
        budget.IsDeleted = true;
        budget.DeletedAt = now;
        budget.UpdatedAt = now;
        budget.UpdatedById = actorUserId;

        await _budgetRepository.UpdateAsync(budget, ct);
        await _budgetRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<BudgetSummaryDto>> GetSummaryAsync(Guid entityNodeId, CancellationToken ct = default)
    {
        var (items, _) = await _budgetRepository.GetPagedAsync(
            entityNodeId, null, null, 1, int.MaxValue, ct);

        var totalBudget = items.Sum(b => b.Amount);
        var totalSpent = items.Sum(b => b.Spent);
        var totalRemaining = totalBudget - totalSpent;
        var activeCount = items.Count(b => b.IsActive);

        return Result.Success(new BudgetSummaryDto(
            totalBudget, totalSpent, totalRemaining, activeCount, items.Count));
    }

    private static BudgetDto MapToDto(Budget budget) => new(
        budget.Id,
        budget.EntityId,
        budget.Name,
        budget.Reference,
        budget.Amount,
        budget.Spent,
        budget.Remaining,
        budget.PercentUsed,
        budget.StartDate,
        budget.EndDate,
        budget.LocationId,
        budget.Comment,
        budget.IsActive,
        budget.ParentBudgetId,
        budget.CreatedAt,
        budget.UpdatedAt);

    private static BudgetListDto MapToListDto(Budget budget) => new(
        budget.Id,
        budget.Name,
        budget.Reference,
        budget.Amount,
        budget.Spent,
        budget.Remaining,
        budget.PercentUsed,
        budget.StartDate,
        budget.EndDate,
        budget.IsActive);

    private static Error BudgetNotFound() => new("BUDGET.NOT_FOUND", "Budget not found.", ErrorType.NotFound);

    private static Error BudgetNameExists(string name)
        => new("BUDGET.NAME_EXISTS", $"A budget named '{name}' already exists.", ErrorType.Conflict);

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
        => (page < 1 ? 1 : page, pageSize switch { < 1 => 20, > 200 => 200, _ => pageSize });
}
