using Daraban.Modules.Financial.Data.Entities;

namespace Daraban.Modules.Financial.Services.Dtos;

public record SupplierDto(
    Guid Id,
    Guid EntityId,
    string Name,
    string? TradingName,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Mobile,
    string? Fax,
    string? Website,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? RegistrationNumber,
    string? VatNumber,
    string? Iban,
    string? BankName,
    string? SortCode,
    SupplierType Type,
    string? Comment,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record SupplierListDto(
    Guid Id,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    SupplierType Type,
    bool IsActive);

public record SupplierPagedResult(
    IReadOnlyList<SupplierListDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record CreateSupplierRequest(
    Guid EntityNodeId,
    string Name,
    string? TradingName,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Mobile,
    string? Fax,
    string? Website,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? RegistrationNumber,
    string? VatNumber,
    string? Iban,
    string? BankName,
    string? SortCode,
    SupplierType Type,
    string? Comment);

public record UpdateSupplierRequest(
    string Name,
    string? TradingName,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Mobile,
    string? Fax,
    string? Website,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? RegistrationNumber,
    string? VatNumber,
    string? Iban,
    string? BankName,
    string? SortCode,
    SupplierType Type,
    string? Comment,
    bool IsActive);
