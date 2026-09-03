using Daraban.Modules.Financial.Data.Entities;

namespace Daraban.Modules.Financial.Services.Dtos;

public record ContractDto(
    Guid Id,
    Guid EntityId,
    string Name,
    string? Reference,
    Guid? ContractTypeId,
    string? ContractTypeName,
    Guid? SupplierId,
    string? SupplierName,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    int? DurationMonths,
    decimal? Value,
    decimal? MonthlyCost,
    decimal? AnnualCost,
    string Currency,
    BillingFrequency BillingFrequency,
    ContractStatus Status,
    bool AutoRenew,
    int? NoticePeriodDays,
    DateTimeOffset? SignedDate,
    Guid? SignedById,
    string? DocumentLocation,
    string? Terms,
    string? Comment,
    bool IsCritical,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record ContractListDto(
    Guid Id,
    string Name,
    string? Reference,
    string? SupplierName,
    string? ContractTypeName,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    decimal? Value,
    ContractStatus Status,
    bool IsCritical);

public record ContractPagedResult(
    IReadOnlyList<ContractListDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record CreateContractRequest(
    Guid EntityNodeId,
    string Name,
    string? Reference,
    Guid? ContractTypeId,
    Guid? SupplierId,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    int? DurationMonths,
    decimal? Value,
    decimal? MonthlyCost,
    decimal? AnnualCost,
    string Currency,
    BillingFrequency BillingFrequency,
    bool AutoRenew,
    int? NoticePeriodDays,
    DateTimeOffset? SignedDate,
    Guid? SignedById,
    string? DocumentLocation,
    string? Terms,
    string? Comment,
    bool IsCritical);

public record UpdateContractRequest(
    string Name,
    string? Reference,
    Guid? ContractTypeId,
    Guid? SupplierId,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    int? DurationMonths,
    decimal? Value,
    decimal? MonthlyCost,
    decimal? AnnualCost,
    string Currency,
    BillingFrequency BillingFrequency,
    bool AutoRenew,
    int? NoticePeriodDays,
    string? DocumentLocation,
    string? Terms,
    string? Comment,
    bool IsCritical);
