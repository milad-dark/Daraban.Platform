using Daraban.Modules.Financial.Data.Entities;
using Daraban.Modules.Financial.Data.Repositories;
using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Modules.Financial.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Services;

public class PurchaseService : IPurchaseService
{
    private readonly IPurchaseRepository _purchaseRepository;

    public PurchaseService(IPurchaseRepository purchaseRepository)
    {
        _purchaseRepository = purchaseRepository;
    }

    public async Task<Result<PurchasePagedResult>> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        PurchaseStatus? status,
        Guid? supplierId,
        Guid? budgetId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _purchaseRepository.GetPagedAsync(
            entityNodeId, search, status, supplierId, budgetId, page, pageSize, ct);

        var dtos = items.Select(MapToListDto).ToList();
        return Result<PurchasePagedResult>.Success(new PurchasePagedResult(dtos, totalCount, page, pageSize));
    }

    public async Task<Result<PurchaseDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var purchase = await _purchaseRepository.GetByIdWithDetailsAsync(id, ct);
        if (purchase is null)
            return Result.Failure<PurchaseDto>(new Error("PURCHASE.NOT_FOUND", "Purchase not found.", ErrorType.NotFound));

        return Result<PurchaseDto>.Success(MapToDto(purchase));
    }

    public async Task<Result<PurchaseDto>> CreateAsync(CreatePurchaseRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        // Validate unique order number
        var orderNumberExists = await _purchaseRepository.OrderNumberExistsAsync(request.OrderNumber, ct);
        if (orderNumberExists)
            return Result.Failure<PurchaseDto>(new Error("PURCHASE.ORDER_EXISTS", "A purchase with this order number already exists.", ErrorType.Conflict));

        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            EntityId = request.EntityNodeId,
            OrderNumber = request.OrderNumber,
            Name = request.Name,
            SupplierId = request.SupplierId,
            BudgetId = request.BudgetId,
            RequestedById = actorUserId,
            TotalAmount = request.TotalAmount,
            TaxAmount = request.TaxAmount,
            Currency = request.Currency,
            ExchangeRate = request.ExchangeRate,
            PaymentTerms = request.PaymentTerms,
            DeliveryAddress = request.DeliveryAddress,
            Comment = request.Comment,
            SupplierQuoteReference = request.SupplierQuoteReference,
            Status = PurchaseStatus.Draft,
            RequestedDate = DateTimeOffset.UtcNow,
            CreatedById = actorUserId,
            UpdatedById = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _purchaseRepository.AddAsync(purchase, ct);
        await _purchaseRepository.SaveChangesAsync(ct);

        return Result<PurchaseDto>.Success(MapToDto(purchase));
    }

    public async Task<Result<PurchaseDto>> UpdateAsync(Guid id, UpdatePurchaseRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var purchase = await _purchaseRepository.GetByIdAsync(id, ct);
        if (purchase is null)
            return Result.Failure<PurchaseDto>(new Error("PURCHASE.NOT_FOUND", "Purchase not found.", ErrorType.NotFound));

        // Only allow updates on certain statuses
        if (purchase.Status != PurchaseStatus.Draft && purchase.Status != PurchaseStatus.PendingApproval)
            return Result.Failure<PurchaseDto>(new Error("PURCHASE.UPDATE_BLOCKED", "Cannot update purchases in current status.", ErrorType.BusinessRule));

        purchase.Name = request.Name;
        purchase.SupplierId = request.SupplierId;
        purchase.BudgetId = request.BudgetId;
        purchase.TotalAmount = request.TotalAmount;
        purchase.TaxAmount = request.TaxAmount;
        purchase.Currency = request.Currency;
        purchase.ExchangeRate = request.ExchangeRate;
        purchase.PaymentTerms = request.PaymentTerms;
        purchase.DeliveryAddress = request.DeliveryAddress;
        purchase.Comment = request.Comment;
        purchase.SupplierQuoteReference = request.SupplierQuoteReference;
        purchase.UpdatedAt = DateTimeOffset.UtcNow;
        purchase.UpdatedById = actorUserId;

        await _purchaseRepository.UpdateAsync(purchase, ct);
        await _purchaseRepository.SaveChangesAsync(ct);

        return Result<PurchaseDto>.Success(MapToDto(purchase));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var purchase = await _purchaseRepository.GetByIdAsync(id, ct);
        if (purchase is null)
            return Result.Failure(new Error("PURCHASE.NOT_FOUND", "Purchase not found.", ErrorType.NotFound));

        // Only allow deletion on certain statuses
        if (purchase.Status != PurchaseStatus.Draft)
            return Result.Failure(new Error("PURCHASE.DELETE_BLOCKED", "Can only delete draft purchases.", ErrorType.BusinessRule));

        // Soft delete
        purchase.IsDeleted = true;
        purchase.DeletedAt = DateTimeOffset.UtcNow;
        purchase.UpdatedAt = DateTimeOffset.UtcNow;
        purchase.UpdatedById = actorUserId;

        await _purchaseRepository.UpdateAsync(purchase, ct);
        await _purchaseRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<PurchaseDto>> ChangeStatusAsync(Guid id, PurchaseStatus newStatus, Guid actorUserId, CancellationToken ct = default)
    {
        var purchase = await _purchaseRepository.GetByIdAsync(id, ct);
        if (purchase is null)
            return Result.Failure<PurchaseDto>(new Error("PURCHASE.NOT_FOUND", "Purchase not found.", ErrorType.NotFound));

        // Validate status transition
        var isValidTransition = IsValidStatusTransition(purchase.Status, newStatus);
        if (!isValidTransition)
            return Result.Failure<PurchaseDto>(new Error("PURCHASE.INVALID_TRANSITION", $"Cannot transition from {purchase.Status} to {newStatus}.", ErrorType.BusinessRule));

        var previousStatus = purchase.Status;
        purchase.Status = newStatus;
        purchase.UpdatedAt = DateTimeOffset.UtcNow;
        purchase.UpdatedById = actorUserId;

        // Set timestamps based on status
        if (newStatus == PurchaseStatus.Approved)
            purchase.ApprovedDate = DateTimeOffset.UtcNow;
        else if (newStatus == PurchaseStatus.Ordered)
            purchase.OrderedDate = DateTimeOffset.UtcNow;
        else if (newStatus == PurchaseStatus.Received)
            purchase.ReceivedDate = DateTimeOffset.UtcNow;

        await _purchaseRepository.UpdateAsync(purchase, ct);
        await _purchaseRepository.SaveChangesAsync(ct);

        return Result<PurchaseDto>.Success(MapToDto(purchase));
    }

    public async Task<Result<PurchaseDto>> AddItemAsync(Guid purchaseId, CreatePurchaseItemRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var purchase = await _purchaseRepository.GetByIdWithDetailsAsync(purchaseId, ct);
        if (purchase is null)
            return Result.Failure<PurchaseDto>(new Error("PURCHASE.NOT_FOUND", "Purchase not found.", ErrorType.NotFound));

        if (purchase.Status != PurchaseStatus.Draft)
            return Result.Failure<PurchaseDto>(new Error("PURCHASE.ADD_ITEM_BLOCKED", "Can only add items to draft purchases.", ErrorType.BusinessRule));

        var item = new PurchaseItem
        {
            Id = Guid.NewGuid(),
            PurchaseId = purchaseId,
            Description = request.Description,
            ItemReference = request.ItemReference,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            DiscountPercent = request.DiscountPercent,
            TaxRate = request.TaxRate,
            AssetId = request.AssetId,
            Comment = request.Comment
        };

        purchase.Items.Add(item);

        // Recalculate total
        purchase.TotalAmount = purchase.Items.Sum(i => i.LineTotal);
        purchase.TaxAmount = purchase.Items.Sum(i => i.TaxAmount);
        purchase.UpdatedAt = DateTimeOffset.UtcNow;
        purchase.UpdatedById = actorUserId;

        await _purchaseRepository.UpdateAsync(purchase, ct);
        await _purchaseRepository.SaveChangesAsync(ct);

        return Result<PurchaseDto>.Success(MapToDto(purchase));
    }

    public async Task<Result<PurchaseDto>> RemoveItemAsync(Guid purchaseId, Guid itemId, Guid actorUserId, CancellationToken ct = default)
    {
        var purchase = await _purchaseRepository.GetByIdWithDetailsAsync(purchaseId, ct);
        if (purchase is null)
            return Result.Failure<PurchaseDto>(new Error("PURCHASE.NOT_FOUND", "Purchase not found.", ErrorType.NotFound));

        if (purchase.Status != PurchaseStatus.Draft)
            return Result.Failure<PurchaseDto>(new Error("PURCHASE.REMOVE_ITEM_BLOCKED", "Can only remove items from draft purchases.", ErrorType.BusinessRule));

        var item = purchase.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Result.Failure<PurchaseDto>(new Error("PURCHASE.ITEM_NOT_FOUND", "Item not found.", ErrorType.NotFound));

        purchase.Items.Remove(item);

        // Recalculate total
        purchase.TotalAmount = purchase.Items.Sum(i => i.LineTotal);
        purchase.TaxAmount = purchase.Items.Sum(i => i.TaxAmount);
        purchase.UpdatedAt = DateTimeOffset.UtcNow;
        purchase.UpdatedById = actorUserId;

        await _purchaseRepository.UpdateAsync(purchase, ct);
        await _purchaseRepository.SaveChangesAsync(ct);

        return Result<PurchaseDto>.Success(MapToDto(purchase));
    }

    private static bool IsValidStatusTransition(PurchaseStatus current, PurchaseStatus next)
    {
        return current switch
        {
            PurchaseStatus.Draft => next is PurchaseStatus.PendingApproval,
            PurchaseStatus.PendingApproval => next is PurchaseStatus.Approved or PurchaseStatus.Cancelled,
            PurchaseStatus.Approved => next is PurchaseStatus.Ordered or PurchaseStatus.Cancelled,
            PurchaseStatus.Ordered => next is PurchaseStatus.PartiallyReceived or PurchaseStatus.Received,
            PurchaseStatus.PartiallyReceived => next is PurchaseStatus.Received,
            PurchaseStatus.Received => false,
            PurchaseStatus.Cancelled => false,
            _ => false
        };
    }

    private static PurchaseDto MapToDto(Purchase purchase) => new(
        purchase.Id,
        purchase.EntityId,
        purchase.OrderNumber,
        purchase.Name,
        purchase.Status,
        purchase.SupplierId,
        purchase.Supplier?.Name,
        purchase.BudgetId,
        purchase.Budget?.Name,
        purchase.RequestedDate,
        purchase.ApprovedDate,
        purchase.RequestedById,
        purchase.ApprovedById,
        purchase.OrderedDate,
        purchase.ExpectedDeliveryDate,
        purchase.ReceivedDate,
        purchase.TotalAmount,
        purchase.TaxAmount,
        purchase.TotalWithTax,
        purchase.Currency,
        purchase.ExchangeRate,
        purchase.PaymentTerms,
        purchase.PaymentMethod,
        purchase.PaymentDate,
        purchase.IsPaid,
        purchase.DeliveryAddress,
        purchase.Comment,
        purchase.SupplierQuoteReference,
        purchase.Items.Select(MapItemToDto).ToList(),
        purchase.CreatedAt,
        purchase.UpdatedAt);

    private static PurchaseListDto MapToListDto(Purchase purchase) => new(
        purchase.Id,
        purchase.OrderNumber,
        purchase.Name,
        purchase.Status,
        purchase.Supplier?.Name,
        purchase.RequestedDate,
        purchase.TotalAmount,
        purchase.IsPaid);

    private static PurchaseItemDto MapItemToDto(PurchaseItem item) => new(
        item.Id,
        item.PurchaseId,
        item.Description,
        item.ItemReference,
        item.Quantity,
        item.UnitPrice,
        item.DiscountPercent,
        item.LineTotal,
        item.TaxRate,
        item.TaxAmount,
        item.TotalWithTax,
        item.AssetId,
        item.Comment);
}
