using System;
using System.Collections.Generic;
using Daraban.Modules.Assets.Data.Entities;
namespace Daraban.Modules.Assets.Services.Dtos;
public record AssetDto(
    Guid Id,
    string Name,
    string? AssetTag,
    string? SerialNumber,
    AssetStatus Status,
    string AssetTypeName,
    string? AssetModelName,
    string? ManufacturerName,
    string? LocationName,
    DateOnly? PurchaseDate,
    decimal? PurchaseCost,
    string? PurchaseCurrency,
    DateOnly? WarrantyExpiry,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
public record AssetListDto(
    Guid Id,
    string Name,
    string? AssetTag,
    string? SerialNumber,
    AssetStatus Status,
    string AssetTypeName,
    string? LocationName,
    DateOnly? WarrantyExpiry);
public record CreateAssetRequest(
    string Name,
    Guid AssetTypeId,
    Guid? AssetModelId,
    Guid? LocationId,
    Guid EntityNodeId,
    string? AssetTag,
    string? SerialNumber,
    DateOnly? PurchaseDate,
    decimal? PurchaseCost,
    string? PurchaseCurrency,
    string? OrderNumber,
    string? SupplierName,
    DateOnly? WarrantyExpiry,
    string? Notes);
public record UpdateAssetRequest(
    string Name,
    Guid? AssetModelId,
    Guid? LocationId,
    string? AssetTag,
    string? SerialNumber,
    DateOnly? PurchaseDate,
    decimal? PurchaseCost,
    string? PurchaseCurrency,
    string? OrderNumber,
    string? SupplierName,
    DateOnly? WarrantyExpiry,
    string? Notes);
public record AssetPagedResult(IReadOnlyList<AssetListDto> Items, int TotalCount, int Page, int PageSize);
public record AssetCategoryDto(Guid Id, Guid? ParentId, string Name, string? Description, int SortOrder);
public record CreateAssetCategoryRequest(Guid? ParentId, string Name, string? Description, int SortOrder = 0);
public record AssetTypeDto(Guid Id, Guid CategoryId, string CategoryName, string Name, string? Description, string? Icon);
public record CreateAssetTypeRequest(Guid CategoryId, string Name, string? Description, string? Icon);
public record LocationDto(Guid Id, Guid? ParentId, string Name, string? City, string? Country);
public record CreateLocationRequest(Guid? ParentId, string Name, string? Address, string? PostalCode, string? City, string? Country);
public record ManufacturerDto(Guid Id, string Name, string? Website, string? SupportUrl, string? SupportPhone);
public record CreateManufacturerRequest(string Name, string? Website, string? SupportUrl, string? SupportPhone);
public record AssignAssetRequest(AssignmentTargetType TargetType, Guid TargetId, string? TargetName, string? Notes);
public record AssetAssignmentDto(Guid Id, AssignmentTargetType TargetType, Guid TargetId, string? TargetName, DateTimeOffset AssignedAt, DateTimeOffset? UnassignedAt, bool IsCurrent, string? Notes);
public record LifecycleTransitionRequest(AssetStatus ToStatus, string? Reason, string? Notes);
public record AssetStatusHistoryDto(Guid Id, AssetStatus FromStatus, AssetStatus ToStatus, Guid ActorUserId, string? Reason, DateTimeOffset OccurredAt);
