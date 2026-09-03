using Daraban.Modules.Software.Data.Entities;

namespace Daraban.Modules.Software.Services.Dtos;

public record SoftwareDto(
    Guid Id,
    Guid EntityId,
    string Name,
    string? Version,
    string? Editor,
    string? Description,
    SoftwareCategory Category,
    string? Edition,
    bool IsActive,
    bool IsOpenSource,
    bool IsFree,
    string? Website,
    string? DocumentationUrl,
    string? Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record SoftwareListDto(
    Guid Id,
    string Name,
    string? Version,
    string? Editor,
    SoftwareCategory Category,
    bool IsActive);

public record SoftwarePagedResult(
    IReadOnlyList<SoftwareListDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record CreateSoftwareRequest(
    Guid EntityNodeId,
    string Name,
    string? Version,
    string? Editor,
    string? Description,
    SoftwareCategory Category,
    string? Edition,
    bool IsOpenSource,
    bool IsFree,
    string? Website,
    string? DocumentationUrl,
    string? Comment);

public record UpdateSoftwareRequest(
    string Name,
    string? Version,
    string? Editor,
    string? Description,
    SoftwareCategory Category,
    string? Edition,
    bool IsActive,
    bool IsOpenSource,
    bool IsFree,
    string? Website,
    string? DocumentationUrl,
    string? Comment);
