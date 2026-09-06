using Daraban.Modules.Financial.Data.Entities;
using Daraban.Modules.Financial.Data.Repositories;
using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Modules.Financial.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Services;

public class InfocomService : IInfocomService
{
    private readonly IInfocomRepository _infocomRepository;

    public InfocomService(IInfocomRepository infocomRepository)
    {
        _infocomRepository = infocomRepository;
    }

    public async Task<Result<InfocomPagedResult>> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        Guid? supplierId,
        Guid? budgetId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);

        var (items, totalCount) = await _infocomRepository.GetPagedAsync(
            entityNodeId, search, supplierId, budgetId, normalizedPage, normalizedPageSize, ct);

        var dtos = items.Select(MapToListDto).ToList();
        return Result.Success(new InfocomPagedResult(dtos, totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<InfocomDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var infocom = await _infocomRepository.GetByIdAsync(id, ct);
        if (infocom is null)
            return Result.Failure<InfocomDto>(new Error("INFOCOM.NOT_FOUND", "Infocom entry not found.", ErrorType.NotFound));

        return Result<InfocomDto>.Success(MapToDto(infocom));
    }

    public async Task<Result<InfocomDto>> GetByAssetIdAsync(Guid assetId, CancellationToken ct = default)
    {
        var infocom = await _infocomRepository.GetByAssetIdAsync(assetId, ct);
        if (infocom is null)
            return Result.Failure<InfocomDto>(new Error("INFOCOM.NOT_FOUND", "No active infocom entry found for this asset.", ErrorType.NotFound));

        return Result<InfocomDto>.Success(MapToDto(infocom));
    }

    public async Task<Result<InfocomDto>> CreateAsync(CreateInfocomRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        // Check if asset already has an active infocom entry
        var hasInfocom = await _infocomRepository.AssetHasInfocomAsync(request.AssetId, ct);
        if (hasInfocom)
            return Result.Failure<InfocomDto>(new Error("INFOCOM.ASSET_EXISTS", "This asset already has an active infocom entry.", ErrorType.Conflict));

        var infocom = new Infocom
        {
            Id = Guid.CreateVersion7(),
            EntityId = request.EntityNodeId,
            AssetId = request.AssetId,
            PurchaseOrderNumber = request.PurchaseOrderNumber,
            InvoiceNumber = request.InvoiceNumber,
            PurchaseDate = request.PurchaseDate,
            DeliveryDate = request.DeliveryDate,
            UseDate = request.UseDate,
            PurchaseCost = request.PurchaseCost,
            AdditionalCost = request.AdditionalCost,
            Currency = request.Currency,
            SupplierId = request.SupplierId,
            BudgetId = request.BudgetId,
            DepreciationMethod = request.DepreciationMethod,
            DepreciationDurationMonths = request.DepreciationDurationMonths,
            DepreciationCoefficient = request.DepreciationCoefficient,
            DepreciationOnUseDate = request.DepreciationOnUseDate,
            CurrentValue = request.PurchaseCost + request.AdditionalCost,
            ResidualValue = request.ResidualValue,
            WarrantyStartDate = request.WarrantyStartDate,
            WarrantyEndDate = request.WarrantyEndDate,
            WarrantyDetails = request.WarrantyDetails,
            InsuranceStartDate = request.InsuranceStartDate,
            InsuranceEndDate = request.InsuranceEndDate,
            InsuranceValue = request.InsuranceValue,
            Comment = request.Comment,
            IsActive = true,
            CreatedById = actorUserId,
            UpdatedById = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _infocomRepository.AddAsync(infocom, ct);
        await _infocomRepository.SaveChangesAsync(ct);

        return Result<InfocomDto>.Success(MapToDto(infocom));
    }

    public async Task<Result<InfocomDto>> UpdateAsync(Guid id, UpdateInfocomRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var infocom = await _infocomRepository.GetByIdAsync(id, ct);
        if (infocom is null)
            return Result.Failure<InfocomDto>(new Error("INFOCOM.NOT_FOUND", "Infocom entry not found.", ErrorType.NotFound));

        infocom.PurchaseOrderNumber = request.PurchaseOrderNumber;
        infocom.InvoiceNumber = request.InvoiceNumber;
        infocom.PurchaseDate = request.PurchaseDate;
        infocom.DeliveryDate = request.DeliveryDate;
        infocom.UseDate = request.UseDate;
        infocom.PurchaseCost = request.PurchaseCost;
        infocom.AdditionalCost = request.AdditionalCost;
        infocom.Currency = request.Currency;
        infocom.SupplierId = request.SupplierId;
        infocom.BudgetId = request.BudgetId;
        infocom.DepreciationMethod = request.DepreciationMethod;
        infocom.DepreciationDurationMonths = request.DepreciationDurationMonths;
        infocom.DepreciationCoefficient = request.DepreciationCoefficient;
        infocom.DepreciationOnUseDate = request.DepreciationOnUseDate;
        infocom.ResidualValue = request.ResidualValue;
        infocom.WarrantyStartDate = request.WarrantyStartDate;
        infocom.WarrantyEndDate = request.WarrantyEndDate;
        infocom.WarrantyDetails = request.WarrantyDetails;
        infocom.InsuranceStartDate = request.InsuranceStartDate;
        infocom.InsuranceEndDate = request.InsuranceEndDate;
        infocom.InsuranceValue = request.InsuranceValue;
        infocom.Comment = request.Comment;
        infocom.UpdatedAt = DateTimeOffset.UtcNow;
        infocom.UpdatedById = actorUserId;

        await _infocomRepository.UpdateAsync(infocom, ct);
        await _infocomRepository.SaveChangesAsync(ct);

        return Result<InfocomDto>.Success(MapToDto(infocom));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var infocom = await _infocomRepository.GetByIdAsync(id, ct);
        if (infocom is null)
            return Result.Failure(new Error("INFOCOM.NOT_FOUND", "Infocom entry not found.", ErrorType.NotFound));

        // Soft delete
        infocom.IsDeleted = true;
        infocom.DeletedAt = DateTimeOffset.UtcNow;
        infocom.UpdatedAt = DateTimeOffset.UtcNow;
        infocom.UpdatedById = actorUserId;

        await _infocomRepository.UpdateAsync(infocom, ct);
        await _infocomRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<DepreciationCalculationResult>> CalculateDepreciationAsync(Guid id, CancellationToken ct = default)
    {
        var infocom = await _infocomRepository.GetByIdAsync(id, ct);
        if (infocom is null)
            return Result.Failure<DepreciationCalculationResult>(new Error(
                "INFOCOM.NOT_FOUND", "Infocom entry not found.", ErrorType.NotFound));

        // "None" means no depreciation tracking: the value stays at cost and nothing is written off.
        // Previously this fell into the default branch and returned the same result, but only after
        // computing (and potentially dividing by) a period that doesn't apply.
        if (infocom.DepreciationMethod == DepreciationMethod.None)
        {
            return Result.Success(new DepreciationCalculationResult(
                id, infocom.AssetId, infocom.TotalCost,
                DepreciationAmount: 0, CurrentValue: infocom.TotalCost,
                infocom.ResidualValue, RemainingMonths: 0, FullyDepreciated: false,
                DepreciationStartDate: infocom.PurchaseDate ?? infocom.UseDate ?? DateTimeOffset.UtcNow,
                infocom.DepreciationMethod));
        }

        var totalCost = infocom.TotalCost;
        var depreciationDate = infocom.DepreciationOnUseDate ? infocom.UseDate : infocom.PurchaseDate;

        if (depreciationDate is null)
            return Result.Failure<DepreciationCalculationResult>(new Error(
                "INFOCOM.NO_DATE", "Depreciation date is required.", ErrorType.Validation));

        // A duration of zero would divide by zero in every method below.
        if (infocom.DepreciationDurationMonths <= 0)
            return Result.Failure<DepreciationCalculationResult>(new Error(
                "INFOCOM.INVALID_DURATION",
                "Depreciation duration must be greater than zero.", ErrorType.Validation));

        var monthsElapsed = (int)((DateTimeOffset.UtcNow - depreciationDate.Value).TotalDays / 30);
        var depreciationMonths = Math.Min(monthsElapsed, infocom.DepreciationDurationMonths);
        var depreciableAmount = totalCost - infocom.ResidualValue;

        decimal depreciationAmount;
        decimal currentValue;

        switch (infocom.DepreciationMethod)
        {
            case DepreciationMethod.StraightLine:
                // Multiply before dividing: 3000/36*36 evaluates left-to-right as 83.33...*36 =
                // 2999.999...9 in decimal arithmetic, while 3000*36/36 is exact. Order matters.
                depreciationAmount = depreciableAmount * depreciationMonths / infocom.DepreciationDurationMonths;
                currentValue = totalCost - depreciationAmount;
                break;

            case DepreciationMethod.DecliningBalance:
                // Standard declining-balance: monthly rate = coefficient / useful life in months.
                // A coefficient of 2.0 is double-declining: 2/36 ≈ 5.56%/month on a 36-month
                // schedule, i.e. twice the straight-line 2.78%. The previous formula (rate/100/12)
                // treated the coefficient as an annual percentage, so the default 2.0 meant ~2%
                // per YEAR -- slower than straight line, which defeats the entire purpose of an
                // accelerated method.
                var rate = infocom.DepreciationCoefficient ?? 2.0m;
                depreciationAmount = 0;
                var remainingValue = totalCost;
                for (var i = 0; i < depreciationMonths; i++)
                {
                    var monthlyDepreciation = remainingValue * rate / infocom.DepreciationDurationMonths;
                    depreciationAmount += monthlyDepreciation;
                    remainingValue -= monthlyDepreciation;
                }
                currentValue = Math.Max(remainingValue, infocom.ResidualValue);
                break;

            case DepreciationMethod.SumOfYearsDigits:
                var sumOfYears = infocom.DepreciationDurationMonths * (infocom.DepreciationDurationMonths + 1) / 2;
                depreciationAmount = 0;
                remainingValue = totalCost;
                for (var i = 0; i < depreciationMonths; i++)
                {
                    var fraction = (infocom.DepreciationDurationMonths - i) / (decimal)sumOfYears;
                    depreciationAmount += depreciableAmount * fraction;
                }
                currentValue = Math.Max(totalCost - depreciationAmount, infocom.ResidualValue);
                break;

            default:
                depreciationAmount = 0;
                currentValue = totalCost;
                break;
        }

        var remainingMonths = infocom.DepreciationDurationMonths - depreciationMonths;
        var fullyDepreciated = remainingMonths <= 0;

        return Result<DepreciationCalculationResult>.Success(new DepreciationCalculationResult(
            id,
            infocom.AssetId,
            totalCost,
            depreciationAmount,
            currentValue,
            infocom.ResidualValue,
            remainingMonths,
            fullyDepreciated,
            depreciationDate.Value,
            infocom.DepreciationMethod));
    }

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
        => (page < 1 ? 1 : page, pageSize switch { < 1 => 20, > 200 => 200, _ => pageSize });

    private static InfocomDto MapToDto(Infocom infocom) => new(
        infocom.Id,
        infocom.EntityId,
        infocom.AssetId,
        infocom.PurchaseOrderNumber,
        infocom.InvoiceNumber,
        infocom.PurchaseDate,
        infocom.DeliveryDate,
        infocom.UseDate,
        infocom.PurchaseCost,
        infocom.AdditionalCost,
        infocom.TotalCost,
        infocom.Currency,
        infocom.SupplierId,
        infocom.Supplier?.Name,
        infocom.BudgetId,
        infocom.Budget?.Name,
        infocom.DepreciationMethod,
        infocom.DepreciationDurationMonths,
        infocom.DepreciationCoefficient,
        infocom.DepreciationOnUseDate,
        infocom.CurrentValue,
        infocom.ResidualValue,
        infocom.WarrantyStartDate,
        infocom.WarrantyEndDate,
        infocom.WarrantyDetails,
        infocom.InsuranceStartDate,
        infocom.InsuranceEndDate,
        infocom.InsuranceValue,
        infocom.Comment,
        infocom.DecommissionDate,
        infocom.SalePrice,
        infocom.IsActive,
        infocom.CreatedAt,
        infocom.UpdatedAt);

    private static InfocomListDto MapToListDto(Infocom infocom) => new(
        infocom.Id,
        infocom.AssetId,
        infocom.PurchaseOrderNumber,
        infocom.InvoiceNumber,
        infocom.PurchaseDate,
        infocom.PurchaseCost,
        infocom.CurrentValue,
        infocom.Supplier?.Name,
        infocom.Budget?.Name);
}
