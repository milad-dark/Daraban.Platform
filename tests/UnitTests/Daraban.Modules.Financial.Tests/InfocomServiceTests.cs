using Daraban.Modules.Financial.Data.Entities;
using Daraban.Modules.Financial.Data.Repositories;
using Daraban.Modules.Financial.Services;
using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.Financial.Tests;

/// <summary>
/// InfocomService: one financial record per asset, plus the depreciation engine. The engine is
/// the part that earns the most scrutiny -- it is pure arithmetic against stored rows, so the
/// expected values in these tests are computed by hand, not by calling back into the code.
/// </summary>
public class InfocomServiceTests
{
    private static readonly Guid EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IInfocomRepository> _infocom = new(MockBehavior.Strict);

    private InfocomService CreateSut() => new(_infocom.Object);

    private static Infocom InfocomWith(
        int durationMonths = 36,
        DepreciationMethod method = DepreciationMethod.StraightLine,
        decimal purchaseCost = 3_600,
        decimal additionalCost = 0,
        decimal residual = 0,
        decimal? coefficient = null,
        bool onUseDate = false,
        DateTimeOffset? purchaseDate = null)
    {
        var bought = purchaseDate ?? DateTimeOffset.UtcNow.AddYears(-2);
        return new Infocom
        {
            Id = Guid.CreateVersion7(),
            EntityId = EntityId,
            AssetId = Guid.CreateVersion7(),
            PurchaseCost = purchaseCost,
            AdditionalCost = additionalCost,
            PurchaseDate = bought,
            UseDate = bought.AddDays(7),
            DepreciationMethod = method,
            DepreciationDurationMonths = durationMonths,
            DepreciationCoefficient = coefficient,
            DepreciationOnUseDate = onUseDate,
            ResidualValue = residual,
            IsActive = true,
            Currency = "USD",
        };
    }

    private static CreateInfocomRequest CreateRequest(Guid? assetId = null)
        => new(EntityId, assetId ?? Guid.CreateVersion7(), "PO-77", "INV-1",
            DateTimeOffset.UtcNow.AddYears(-1), null, null, 3_600, 400, "USD", null, null,
            DepreciationMethod.StraightLine, 36, null, false, 0m, null, null, null, null, null, null, null);

    // ---- Create ------------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Rejects_A_Second_Record_For_The_Same_Asset()
    {
        var assetId = Guid.CreateVersion7();
        _infocom.Setup(r => r.AssetHasInfocomAsync(assetId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().CreateAsync(CreateRequest(assetId), ActorId);

        // One asset, one financial record -- a second row would split the value across two ledgers.
        Assert.False(result.IsSuccess);
        Assert.Equal("INFOCOM.ASSET_EXISTS", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        _infocom.Verify(r => r.AddAsync(It.IsAny<Infocom>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Seeds_CurrentValue_At_Total_Cost()
    {
        Infocom? captured = null;
        _infocom.Setup(r => r.AssetHasInfocomAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _infocom.Setup(r => r.AddAsync(It.IsAny<Infocom>(), It.IsAny<CancellationToken>()))
            .Callback<Infocom, CancellationToken>((i, _) => captured = i)
            .Returns(Task.CompletedTask);
        _infocom.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(CreateRequest(), ActorId);

        Assert.True(result.IsSuccess);

        // A brand-new record has depreciated by nothing yet: 3,600 + 400 = 4,000.
        Assert.Equal(4_000, captured!.CurrentValue);
        Assert.True(captured.IsActive);
        Assert.Equal(EntityId, captured.EntityId);
        Assert.Equal(7, captured.Id.Version); // UUIDv7, not v4
    }

    // ---- Depreciation: StraightLine ----------------------------------------------------------

    [Fact]
    public async Task CalculateDepreciation_StraightLine_Depreciates_Linearly()
    {
        // Bought 24 months for a 3,600 asset over 36 months with no residual:
        // 3,600 / 36 x 24 = 2,400 written off, 1,200 remaining, 12 months left.
        var bought = DateTimeOffset.UtcNow.AddDays(-24 * 30);
        var infocom = InfocomWith(durationMonths: 36, method: DepreciationMethod.StraightLine,
            purchaseCost: 3_600, purchaseDate: bought);

        _infocom.Setup(r => r.GetByIdAsync(infocom.Id, It.IsAny<CancellationToken>())).ReturnsAsync(infocom);

        var result = await CreateSut().CalculateDepreciationAsync(infocom.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(3_600, result.Value.TotalCost);
        Assert.Equal(2_400, result.Value.DepreciationAmount);
        Assert.Equal(1_200, result.Value.CurrentValue);
        Assert.Equal(12, result.Value.RemainingMonths);
        Assert.False(result.Value.FullyDepreciated);
        Assert.Equal(DepreciationMethod.StraightLine, result.Value.Method);
    }

    [Fact]
    public async Task CalculateDepreciation_StraightLine_Clamps_At_Full_Term()
    {
        // Five years into a three-year schedule: the write-off stops at 100%, it must not exceed
        // the cost.
        var infocom = InfocomWith(durationMonths: 36, method: DepreciationMethod.StraightLine,
            purchaseCost: 3_600, purchaseDate: DateTimeOffset.UtcNow.AddYears(-5));

        _infocom.Setup(r => r.GetByIdAsync(infocom.Id, It.IsAny<CancellationToken>())).ReturnsAsync(infocom);

        var result = await CreateSut().CalculateDepreciationAsync(infocom.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(3_600, result.Value.DepreciationAmount);
        Assert.Equal(0, result.Value.CurrentValue);
        Assert.Equal(0, result.Value.RemainingMonths);
        Assert.True(result.Value.FullyDepreciated);
    }

    [Fact]
    public async Task CalculateDepreciation_StraightLine_Respects_The_Residual_Floor()
    {
        var infocom = InfocomWith(durationMonths: 36, method: DepreciationMethod.StraightLine,
            purchaseCost: 3_600, residual: 600, purchaseDate: DateTimeOffset.UtcNow.AddYears(-5));

        _infocom.Setup(r => r.GetByIdAsync(infocom.Id, It.IsAny<CancellationToken>())).ReturnsAsync(infocom);

        var result = await CreateSut().CalculateDepreciationAsync(infocom.Id);

        // Only (3,600 - 600) = 3,000 is depreciable; the residual 600 is untouchable.
        Assert.True(result.IsSuccess);
        Assert.Equal(3_000, result.Value.DepreciationAmount);
        Assert.Equal(600, result.Value.CurrentValue);
        Assert.True(result.Value.FullyDepreciated);
    }

    // ---- Depreciation: DecliningBalance ------------------------------------------------------

    [Fact]
    public async Task CalculateDepreciation_DecliningBalance_Writes_Off_More_Early_Than_Linear()
    {
        // Same asset on both methods, halfway through a 36-month schedule with a 200% coefficient:
        // declining balance must be ahead of the linear 50% at this point -- that acceleration is
        // the entire reason the method exists. (With the old rate/100/12 formula it would have
        // been behind, depreciating at ~2% per year.)
        var bought = DateTimeOffset.UtcNow.AddDays(-18 * 30);
        const decimal cost = 3_600m;

        var infocom = InfocomWith(durationMonths: 36, method: DepreciationMethod.DecliningBalance,
            purchaseCost: cost, coefficient: 2.0m, purchaseDate: bought);

        _infocom.Setup(r => r.GetByIdAsync(infocom.Id, It.IsAny<CancellationToken>())).ReturnsAsync(infocom);

        var result = await CreateSut().CalculateDepreciationAsync(infocom.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.DepreciationAmount > cost / 2,
            $"DB should be ahead of linear halfway through; got {result.Value.DepreciationAmount} of {cost}");
        Assert.True(result.Value.CurrentValue >= 0);
        Assert.Equal(DepreciationMethod.DecliningBalance, result.Value.Method);
    }

    [Fact]
    public async Task CalculateDepreciation_DecliningBalance_Defaults_To_A_200_Percent_Coefficient()
    {
        var infocom = InfocomWith(durationMonths: 36, method: DepreciationMethod.DecliningBalance,
            purchaseCost: 3_600, coefficient: null, purchaseDate: DateTimeOffset.UtcNow.AddDays(-18 * 30));

        _infocom.Setup(r => r.GetByIdAsync(infocom.Id, It.IsAny<CancellationToken>())).ReturnsAsync(infocom);

        var result = await CreateSut().CalculateDepreciationAsync(infocom.Id);

        // coefficient ?? 2.0: without a stored value the engine assumes double-declining.
        Assert.True(result.IsSuccess);
        Assert.Equal(2.0m, infocom.DepreciationCoefficient ?? 2.0m);
        Assert.True(result.Value.DepreciationAmount > 0);
    }

    [Fact]
    public async Task CalculateDepreciation_DecliningBalance_Never_Breaches_The_Residual()
    {
        var infocom = InfocomWith(durationMonths: 36, method: DepreciationMethod.DecliningBalance,
            purchaseCost: 3_600, residual: 1_800, coefficient: 2.0m,
            purchaseDate: DateTimeOffset.UtcNow.AddYears(-10));

        _infocom.Setup(r => r.GetByIdAsync(infocom.Id, It.IsAny<CancellationToken>())).ReturnsAsync(infocom);

        var result = await CreateSut().CalculateDepreciationAsync(infocom.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1_800, result.Value.CurrentValue);
        Assert.True(result.Value.FullyDepreciated);
    }

    // ---- Depreciation: SumOfYearsDigits ------------------------------------------------------

    [Fact]
    public async Task CalculateDepreciation_SumOfYearsDigits_FrontLoads_The_Write_Off()
    {
        // 36-month schedule: first month's fraction is 36/666 of 3,600 = ~194.6; the linear amount
        // would be 100. After one month SOYD must already be ahead of linear.
        var bought = DateTimeOffset.UtcNow.AddDays(-30);
        var infocom = InfocomWith(durationMonths: 36, method: DepreciationMethod.SumOfYearsDigits,
            purchaseCost: 3_600, purchaseDate: bought);

        _infocom.Setup(r => r.GetByIdAsync(infocom.Id, It.IsAny<CancellationToken>())).ReturnsAsync(infocom);

        var result = await CreateSut().CalculateDepreciationAsync(infocom.Id);

        Assert.True(result.IsSuccess);
        Assert.InRange(result.Value.DepreciationAmount, 190m, 200m);
        Assert.True(result.Value.CurrentValue < 3_500);
    }

    [Fact]
    public async Task CalculateDepreciation_SumOfYearsDigits_Sums_To_The_Full_Cost()
    {
        var infocom = InfocomWith(durationMonths: 36, method: DepreciationMethod.SumOfYearsDigits,
            purchaseCost: 3_600, purchaseDate: DateTimeOffset.UtcNow.AddYears(-5));

        _infocom.Setup(r => r.GetByIdAsync(infocom.Id, It.IsAny<CancellationToken>())).ReturnsAsync(infocom);

        var result = await CreateSut().CalculateDepreciationAsync(infocom.Id);

        // The digit fractions sum to exactly 1, so a finished schedule writes off the whole cost.
        Assert.True(result.IsSuccess);
        Assert.Equal(3_600, result.Value.DepreciationAmount, precision: 0);
        Assert.True(result.Value.FullyDepreciated);
    }

    // ---- Depreciation: edges -----------------------------------------------------------------

    [Fact]
    public async Task CalculateDepreciation_None_Returns_Cost_Unchanged()
    {
        var infocom = InfocomWith(method: DepreciationMethod.None, purchaseCost: 3_600,
            purchaseDate: DateTimeOffset.UtcNow.AddYears(-5));

        _infocom.Setup(r => r.GetByIdAsync(infocom.Id, It.IsAny<CancellationToken>())).ReturnsAsync(infocom);

        var result = await CreateSut().CalculateDepreciationAsync(infocom.Id);

        // "No depreciation tracking" used to fall into the default branch, which happened to give
        // the same answer -- but only by accident, after computing against a period that does not
        // apply. Now it returns early, before any date/duration logic runs.
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.DepreciationAmount);
        Assert.Equal(3_600, result.Value.CurrentValue);
        Assert.False(result.Value.FullyDepreciated);
    }

    [Fact]
    public async Task CalculateDepreciation_Rejects_A_Zero_Duration_Instead_Of_Dividing_By_Zero()
    {
        var infocom = InfocomWith(durationMonths: 0, method: DepreciationMethod.StraightLine,
            purchaseDate: DateTimeOffset.UtcNow.AddYears(-1));

        _infocom.Setup(r => r.GetByIdAsync(infocom.Id, It.IsAny<CancellationToken>())).ReturnsAsync(infocom);

        var result = await CreateSut().CalculateDepreciationAsync(infocom.Id);

        // Every method divides by duration. A zero-month schedule used to throw
        // DivideByZeroException -- a 500 with no usable message.
        Assert.False(result.IsSuccess);
        Assert.Equal("INFOCOM.INVALID_DURATION", result.Error!.Code);
    }

    [Fact]
    public async Task CalculateDepreciation_Rejects_A_Missing_Date()
    {
        var infocom = InfocomWith(purchaseDate: DateTimeOffset.UtcNow.AddYears(-1));
        infocom.PurchaseDate = null;
        infocom.UseDate = null;

        _infocom.Setup(r => r.GetByIdAsync(infocom.Id, It.IsAny<CancellationToken>())).ReturnsAsync(infocom);

        var result = await CreateSut().CalculateDepreciationAsync(infocom.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("INFOCOM.NO_DATE", result.Error!.Code);
    }

    [Fact]
    public async Task CalculateDepreciation_Uses_UseDate_When_Configured()
    {
        var purchaseDate = DateTimeOffset.UtcNow.AddYears(-2);
        var useDate = DateTimeOffset.UtcNow.AddDays(-6 * 30); // in service 6 months

        var infocom = InfocomWith(durationMonths: 36, method: DepreciationMethod.StraightLine,
            purchaseCost: 3_600, onUseDate: true, purchaseDate: purchaseDate);
        infocom.UseDate = useDate;

        _infocom.Setup(r => r.GetByIdAsync(infocom.Id, It.IsAny<CancellationToken>())).ReturnsAsync(infocom);

        var result = await CreateSut().CalculateDepreciationAsync(infocom.Id);

        Assert.True(result.IsSuccess);

        // ~6 months of 36 = ~600, not ~2,400 from the purchase date.
        Assert.Equal(useDate.Date, result.Value.DepreciationStartDate.Date);
        Assert.InRange(result.Value.DepreciationAmount, 500m, 700m);
    }

    [Fact]
    public async Task CalculateDepreciation_Returns_NotFound_For_A_Missing_Entry()
    {
        var id = Guid.CreateVersion7();
        _infocom.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Infocom?)null);

        var result = await CreateSut().CalculateDepreciationAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("INFOCOM.NOT_FOUND", result.Error!.Code);
    }

    // ---- Reads -------------------------------------------------------------------------------

    [Fact]
    public async Task GetByAssetIdAsync_Returns_NotFound_When_No_Record_Exists()
    {
        var assetId = Guid.CreateVersion7();
        _infocom.Setup(r => r.GetByAssetIdAsync(assetId, It.IsAny<CancellationToken>())).ReturnsAsync((Infocom?)null);

        var result = await CreateSut().GetByAssetIdAsync(assetId);

        Assert.False(result.IsSuccess);
        Assert.Equal("INFOCOM.NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        var infocom = InfocomWith();
        _infocom.Setup(r => r.GetByIdAsync(infocom.Id, It.IsAny<CancellationToken>())).ReturnsAsync(infocom);
        _infocom.Setup(r => r.UpdateAsync(infocom, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _infocom.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().DeleteAsync(infocom.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(infocom.IsDeleted);
        Assert.NotNull(infocom.DeletedAt);
        Assert.Equal(ActorId, infocom.UpdatedById);
    }
}