using Daraban.Modules.Financial.Data.Entities;

namespace Daraban.Modules.Financial.Services.Dtos;

public record PurchaseDto(
    Guid Id,
    Guid EntityId,
    string OrderNumber,
    string Name,
    PurchaseStatus Status,
    Guid? SupplierId,
    string? SupplierName,
    Guid? BudgetId,
    string? BudgetName,
    DateTimeOffset RequestedDate,
    DateTimeOffset? ApprovedDate,
    Guid RequestedById,
    Guid? ApprovedById,
    DateTimeOffset? OrderedDate,
    DateTimeOffset? ExpectedDeliveryDate,
    DateTimeOffset? ReceivedDate,
    decimal TotalAmount,
    decimal TaxAmount,
    decimal TotalWithTax,
    string Currency,
    decimal? ExchangeRate,
    string? PaymentTerms,
    PaymentMethod? PaymentMethod,
    DateTimeOffset? PaymentDate,
    bool IsPaid,
    string? DeliveryAddress,
    string? Comment,
    string? SupplierQuoteReference,
    IReadOnlyList<PurchaseItemDto> Items,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record PurchaseListDto(
    Guid Id,
    string OrderNumber,
    string Name,
    PurchaseStatus Status,
    string? SupplierName,
    DateTimeOffset RequestedDate,
    decimal TotalAmount,
    bool IsPaid);

public record PurchasePagedResult(
    IReadOnlyList<PurchaseListDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record PurchaseItemDto(
    Guid Id,
    Guid PurchaseId,
    string Description,
    string? ItemReference,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal LineTotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal TotalWithTax,
    Guid? AssetId,
    string? Comment);

public record CreatePurchaseRequest(
    Guid EntityNodeId,
    string OrderNumber,
    string Name,
    Guid? SupplierId,
    Guid? BudgetId,
    decimal TotalAmount,
    decimal TaxAmount,
    string Currency,
    decimal? ExchangeRate,
    string? PaymentTerms,
    string? DeliveryAddress,
    string? Comment,
    string? SupplierQuoteReference);

public record UpdatePurchaseRequest(
    string Name,
    Guid? SupplierId,
    Guid? BudgetId,
    decimal TotalAmount,
    decimal TaxAmount,
    string Currency,
    decimal? ExchangeRate,
    string? PaymentTerms,
    string? DeliveryAddress,
    string? Comment,
    string? SupplierQuoteReference);

public record CreatePurchaseItemRequest(
    string Description,
    string? ItemReference,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate,
    Guid? AssetId,
    string? Comment);
