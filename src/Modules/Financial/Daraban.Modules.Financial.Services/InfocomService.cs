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
        var (items, totalCount) = await _infocomRepository.GetPagedAsync(
            entityNodeId, search, supplierId, budgetId, page, pageSize, ct);

        var dtos = items.Select(MapToListDto).ToList();
        return Result<InfocomPagedResult>.Success(new InfocomPagedResult(dtos, totalCount, page, pageSize));
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
            Id = Guid.NewGuid(),
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
            return Result.Failure<DepreciationCalculationResult>(new Error("INFOCOM.NOT_FOUND", "Infocom entry not found.", ErrorType.NotFound));

        var totalCost = infocom.TotalCost;
        var depreciationDate = infocom.DepreciationOnUseDate ? infocom.UseDate : infocom.PurchaseDate;
        
        if (depreciationDate is null)
            return Result.Failure<DepreciationCalculationResult>(new Error("INFOCOM.NO_DATE", "Depreciation date is required.", ErrorType.Validation));

        var monthsElapsed = (int)((DateTimeOffset.UtcNow - depreciationDate.Value).TotalDays / 30);
        var depreciationMonths = Math.Min(monthsElapsed, infocom.DepreciationDurationMonths);
        var depreciableAmount = totalCost - infocom.ResidualValue;

        decimal depreciationAmount;
        decimal currentValue;

        switch (infocom.DepreciationMethod)
        {
            case DepreciationMethod.StraightLine:
                depreciationAmount = depreciableAmount / infocom.DepreciationDurationMonths * depreciationMonths;
                currentValue = totalCost - depreciationAmount;
                break;

            case DepreciationMethod.DecliningBalance:
                var rate = infocom.DepreciationCoefficient ?? 2.0m;
                depreciationAmount = 0;
                var remainingValue = totalCost;
                for (var i = 0; i < depreciationMonths; i++)
                {
                    var yearlyDepreciation = remainingValue * rate / 100 / 12;
                    depreciationAmount += yearlyDepreciation;
                    remainingValue -= yearlyDepreciation;
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
