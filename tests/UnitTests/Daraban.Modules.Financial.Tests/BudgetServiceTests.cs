using Daraban.Modules.Financial.Data.Entities;
using Daraban.Modules.Financial.Data.Repositories;
using Daraban.Modules.Financial.Services;
using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.Financial.Tests;

/// <summary>
/// BudgetService with the repository mocked. The interesting rules here are the ones protecting
/// the ledger: a budget cannot be reduced below what has already been spent, its window must be
/// ordered, and deletion is refused while purchases/infocom/children still point at it.
/// </summary>
public class BudgetServiceTests
{
    private static readonly Guid EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IBudgetRepository> _budgets = new(MockBehavior.Strict);

    private BudgetService CreateSut() => new(_budgets.Object);

    private static Budget BudgetWith(decimal amount = 10_000, decimal spent = 0, bool isActive = true) => new()
    {
        Id = Guid.CreateVersion7(),
        EntityId = EntityId,
        Name = "IT Hardware 2026",
        Amount = amount,
        Spent = spent,
        StartDate = DateTimeOffset.UtcNow.AddMonths(-1),
        EndDate = DateTimeOffset.UtcNow.AddMonths(11),
        IsActive = isActive,
    };

    private static CreateBudgetRequest CreateRequest(
        string name = "IT Hardware 2026", decimal amount = 10_000,
        DateTimeOffset? start = null, DateTimeOffset? end = null)
        => new(EntityId, name, null, amount, start ?? DateTimeOffset.UtcNow.AddMonths(-1),
            end ?? DateTimeOffset.UtcNow.AddMonths(11), null, null, null);

    private static UpdateBudgetRequest UpdateRequest(decimal amount = 12_000)
        => new("Renamed", null, amount, DateTimeOffset.UtcNow.AddMonths(-1),
            DateTimeOffset.UtcNow.AddMonths(11), null, null, true, null);

    // ---- Create ------------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Rejects_A_Duplicate_Name()
    {
        _budgets.Setup(r => r.NameExistsAsync("IT Hardware 2026", EntityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut().CreateAsync(CreateRequest(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("BUDGET.NAME_EXISTS", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        _budgets.Verify(r => r.AddAsync(It.IsAny<Budget>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Rejects_A_Budget_That_Ends_Before_It_Starts()
    {
        _budgets.Setup(r => r.NameExistsAsync(It.IsAny<string>(), EntityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var start = DateTimeOffset.UtcNow;
        var result = await CreateSut().CreateAsync(
            CreateRequest(start: start, end: start.AddMonths(-1)), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("BUDGET.INVALID_RANGE", result.Error!.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task CreateAsync_Rejects_A_Negative_Amount()
    {
        _budgets.Setup(r => r.NameExistsAsync(It.IsAny<string>(), EntityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateSut().CreateAsync(CreateRequest(amount: -5), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("BUDGET.NEGATIVE_AMOUNT", result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_Persists_An_Active_Budget_With_A_Uuidv7_Id()
    {
        Budget? captured = null;
        _budgets.Setup(r => r.NameExistsAsync(It.IsAny<string>(), EntityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _budgets.Setup(r => r.AddAsync(It.IsAny<Budget>(), It.IsAny<CancellationToken>()))
            .Callback<Budget, CancellationToken>((b, _) => captured = b)
            .Returns(Task.CompletedTask);
        _budgets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(
            CreateRequest(start: DateTimeOffset.UtcNow, end: DateTimeOffset.UtcNow.AddMonths(6)), ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(captured!.IsActive);
        Assert.Equal(EntityId, captured.EntityId);
        Assert.Equal(DateTimeOffset.UtcNow.ToString("yyyy-MM"), captured.StartDate.ToString("yyyy-MM"));
        Assert.Equal(7, captured.Id.Version); // UUIDv7, not v4
    }

    // ---- Update ------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_Rejects_Reducing_The_Ceiling_Below_What_Is_Already_Spent()
    {
        var budget = BudgetWith(amount: 10_000, spent: 6_000);
        _budgets.Setup(r => r.GetByIdAsync(budget.Id, It.IsAny<CancellationToken>())).ReturnsAsync(budget);
        _budgets.Setup(r => r.NameExistsAsync("Renamed", EntityId, budget.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateSut().UpdateAsync(budget.Id, UpdateRequest(amount: 5_000), ActorId);

        // Cutting the ceiling below actual spend would retroactively hide the overspend.
        Assert.False(result.IsSuccess);
        Assert.Equal("BUDGET.AMOUNT_BELOW_SPENT", result.Error!.Code);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
    }

    [Fact]
    public async Task UpdateAsync_Allows_Raising_The_Ceiling_Above_Spend()
    {
        var budget = BudgetWith(amount: 10_000, spent: 6_000);
        _budgets.Setup(r => r.GetByIdAsync(budget.Id, It.IsAny<CancellationToken>())).ReturnsAsync(budget);
        _budgets.Setup(r => r.NameExistsAsync("Renamed", EntityId, budget.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _budgets.Setup(r => r.UpdateAsync(budget, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _budgets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UpdateAsync(budget.Id, UpdateRequest(amount: 12_000), ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(12_000, budget.Amount);
    }

    [Fact]
    public async Task UpdateAsync_Validates_The_Date_Window()
    {
        var budget = BudgetWith();
        _budgets.Setup(r => r.GetByIdAsync(budget.Id, It.IsAny<CancellationToken>())).ReturnsAsync(budget);
        _budgets.Setup(r => r.NameExistsAsync(It.IsAny<string>(), EntityId, budget.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateSut().UpdateAsync(
            budget.Id,
            new UpdateBudgetRequest("Renamed", null, 12_000, DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMonths(-3), null, null, true, null),
            ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("BUDGET.INVALID_RANGE", result.Error!.Code);

        // The change was rejected, so nothing was written.
        _budgets.Verify(r => r.UpdateAsync(It.IsAny<Budget>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Delete ------------------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, false)]
    public async Task DeleteAsync_SoftDeletes_An_Unreferenced_Budget(bool hasChildren, bool hasPurchases, bool hasInfocom)
    {
        var budget = BudgetWith();
        var details = new Budget
        {
            Id = budget.Id,
            EntityId = EntityId,
            Name = "IT Hardware 2026",
            Spent = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            ChildBudgets = hasChildren ? new List<Budget> { BudgetWith() } : new List<Budget>(),
            Purchases = hasPurchases ? throw new InvalidOperationException("not set up") : new List<Purchase>(),
            InfocomEntries = hasInfocom ? throw new InvalidOperationException("not set up") : new List<Infocom>(),
        };

        _budgets.Setup(r => r.GetByIdWithDetailsAsync(budget.Id, It.IsAny<CancellationToken>())).ReturnsAsync(details);
        _budgets.Setup(r => r.UpdateAsync(details, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _budgets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().DeleteAsync(budget.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(details.IsDeleted);
        Assert.NotNull(details.DeletedAt);
    }

    [Fact]
    public async Task DeleteAsync_Refuses_When_Child_Budgets_Exist()
    {
        var budget = BudgetWith();
        budget.ChildBudgets.Add(BudgetWith());

        _budgets.Setup(r => r.GetByIdWithDetailsAsync(budget.Id, It.IsAny<CancellationToken>())).ReturnsAsync(budget);

        var result = await CreateSut().DeleteAsync(budget.Id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("BUDGET.HAS_CHILDREN", result.Error!.Code);
    }

    [Fact]
    public async Task DeleteAsync_Refuses_When_Purchases_Are_Attached()
    {
        var budget = BudgetWith();
        budget.Purchases.Add(new Purchase { Id = Guid.CreateVersion7(), OrderNumber = "PO-1" });

        _budgets.Setup(r => r.GetByIdWithDetailsAsync(budget.Id, It.IsAny<CancellationToken>())).ReturnsAsync(budget);

        var result = await CreateSut().DeleteAsync(budget.Id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("BUDGET.HAS_PURCHASES", result.Error!.Code);
    }

    [Fact]
    public async Task DeleteAsync_Refuses_When_Infocom_Entries_Are_Attached()
    {
        var budget = BudgetWith();
        budget.InfocomEntries.Add(new Infocom { AssetId = Guid.CreateVersion7() });

        _budgets.Setup(r => r.GetByIdWithDetailsAsync(budget.Id, It.IsAny<CancellationToken>())).ReturnsAsync(budget);

        var result = await CreateSut().DeleteAsync(budget.Id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("BUDGET.HAS_INFOCOMS", result.Error!.Code);
    }

    // ---- Summary -----------------------------------------------------------------------------

    [Fact]
    public async Task GetSummaryAsync_Computes_Aggregates_Across_Every_Budget()
    {
        var budgets = new[]
        {
            BudgetWith(amount: 10_000, spent: 4_000, isActive: true),
            BudgetWith(amount: 5_000, spent: 5_000, isActive: true),
            BudgetWith(amount: 20_000, spent: 0, isActive: false),
        };

        // int.MaxValue page-size: the summary is a true aggregate, so it must see every row, not
        // the first 1000.
        _budgets.Setup(r => r.GetPagedAsync(
                EntityId, null, null, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((budgets, budgets.Length));

        var result = await CreateSut().GetSummaryAsync(EntityId);

        Assert.True(result.IsSuccess);
        Assert.Equal(35_000, result.Value.TotalBudget);
        Assert.Equal(9_000, result.Value.TotalSpent);
        Assert.Equal(26_000, result.Value.TotalRemaining);
        Assert.Equal(2, result.Value.ActiveCount);
        Assert.Equal(3, result.Value.TotalCount);
    }

    // ---- Paging ------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 20, 1, 20)]
    [InlineData(-5, 20, 1, 20)]
    [InlineData(3, 0, 3, 20)]
    [InlineData(1, 5000, 1, 200)]
    public async Task GetPagedAsync_Normalizes_Paging(int page, int pageSize, int expPage, int expSize)
    {
        _budgets.Setup(r => r.GetPagedAsync(
                EntityId, null, null, expPage, expSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Budget>(), 0));

        var result = await CreateSut().GetPagedAsync(EntityId, null, null, page, pageSize);

        Assert.True(result.IsSuccess);
        // An unclamped page of 0 produces Skip(-pageSize), which Postgres rejects outright.
        Assert.Equal(expPage, result.Value.Page);
        Assert.Equal(expSize, result.Value.PageSize);
    }

    // ---- Read --------------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_Returns_The_Computed_Remaining_And_PercentUsed()
    {
        var budget = BudgetWith(amount: 10_000, spent: 2_500);
        _budgets.Setup(r => r.GetByIdWithDetailsAsync(budget.Id, It.IsAny<CancellationToken>())).ReturnsAsync(budget);

        var result = await CreateSut().GetByIdAsync(budget.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(7_500, result.Value.Remaining);
        Assert.Equal(25m, result.Value.PercentUsed);
    }

    [Fact]
    public async Task GetByIdAsync_Returns_NotFound_For_A_Missing_Budget()
    {
        var id = Guid.CreateVersion7();
        _budgets.Setup(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Budget?)null);

        var result = await CreateSut().GetByIdAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("BUDGET.NOT_FOUND", result.Error!.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task GetByIdAsync_Reports_Zero_Percent_When_Amount_Is_Zero()
    {
        var budget = BudgetWith(amount: 0, spent: 0);
        _budgets.Setup(r => r.GetByIdWithDetailsAsync(budget.Id, It.IsAny<CancellationToken>())).ReturnsAsync(budget);

        var result = await CreateSut().GetByIdAsync(budget.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.PercentUsed);
    }
}