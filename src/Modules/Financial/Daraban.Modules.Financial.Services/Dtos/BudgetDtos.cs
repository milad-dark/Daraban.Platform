namespace Daraban.Modules.Financial.Services.Dtos;

public record BudgetDto(
    Guid Id,
    Guid EntityId,
    string Name,
    string? Reference,
    decimal Amount,
    decimal Spent,
    decimal Remaining,
    decimal PercentUsed,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    Guid? LocationId,
    string? Comment,
    bool IsActive,
    Guid? ParentBudgetId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record BudgetListDto(
    Guid Id,
    string Name,
    string? Reference,
    decimal Amount,
    decimal Spent,
    decimal Remaining,
    decimal PercentUsed,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    bool IsActive);

public record BudgetPagedResult(
    IReadOnlyList<BudgetListDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record BudgetSummaryDto(
    decimal TotalBudget,
    decimal TotalSpent,
    decimal TotalRemaining,
    int ActiveCount,
    int TotalCount);

public record CreateBudgetRequest(
    Guid EntityNodeId,
    string Name,
    string? Reference,
    decimal Amount,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    Guid? LocationId,
    string? Comment,
    Guid? ParentBudgetId);

public record UpdateBudgetRequest(
    string Name,
    string? Reference,
    decimal Amount,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    Guid? LocationId,
    string? Comment,
    bool IsActive,
    Guid? ParentBudgetId);
