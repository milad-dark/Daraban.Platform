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
        var (items, totalCount) = await _budgetRepository.GetPagedAsync(
            entityNodeId, search, isActive, page, pageSize, ct);

        var dtos = items.Select(MapToListDto).ToList();
        return Result<BudgetPagedResult>.Success(new BudgetPagedResult(dtos, totalCount, page, pageSize));
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
            return Result.Failure<BudgetDto>(new Error("BUDGET.NAME_EXISTS", "A budget with this name already exists.", ErrorType.Conflict));

        var budget = new Budget
        {
            Id = Guid.NewGuid(),
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

        return Result<BudgetDto>.Success(MapToDto(budget));
    }

    public async Task<Result<BudgetDto>> UpdateAsync(Guid id, UpdateBudgetRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var budget = await _budgetRepository.GetByIdAsync(id, ct);
        if (budget is null)
            return Result.Failure<BudgetDto>(new Error("BUDGET.NOT_FOUND", "Budget not found.", ErrorType.NotFound));

        // Validate unique name (excluding current budget)
        var nameExists = await _budgetRepository.NameExistsAsync(request.Name, budget.EntityId, id, ct);
        if (nameExists)
            return Result.Failure<BudgetDto>(new Error("BUDGET.NAME_EXISTS", "A budget with this name already exists.", ErrorType.Conflict));

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

        return Result<BudgetDto>.Success(MapToDto(budget));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var budget = await _budgetRepository.GetByIdAsync(id, ct);
        if (budget is null)
            return Result.Failure(new Error("BUDGET.NOT_FOUND", "Budget not found.", ErrorType.NotFound));

        // Soft delete
        budget.IsDeleted = true;
        budget.DeletedAt = DateTimeOffset.UtcNow;
        budget.UpdatedAt = DateTimeOffset.UtcNow;
        budget.UpdatedById = actorUserId;

        await _budgetRepository.UpdateAsync(budget, ct);
        await _budgetRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<BudgetSummaryDto>> GetSummaryAsync(Guid entityNodeId, CancellationToken ct = default)
    {
        var (items, totalCount) = await _budgetRepository.GetPagedAsync(
            entityNodeId, null, null, 1, 1000, ct);

        var totalBudget = items.Sum(b => b.Amount);
        var totalSpent = items.Sum(b => b.Spent);
        var totalRemaining = totalBudget - totalSpent;
        var activeCount = items.Count(b => b.IsActive);

        return Result<BudgetSummaryDto>.Success(new BudgetSummaryDto(
            totalBudget, totalSpent, totalRemaining, activeCount, totalCount));
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
}
