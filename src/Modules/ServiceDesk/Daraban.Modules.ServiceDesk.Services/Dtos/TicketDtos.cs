using Daraban.Modules.ServiceDesk.Data.Entities;

namespace Daraban.Modules.ServiceDesk.Services.Dtos;

// ---- Ticket DTOs ----
public record TicketDto(
    Guid Id,
    TicketType Type,
    TicketStatus Status,
    TicketPriority Priority,
    TicketImpact Impact,
    TicketUrgency Urgency,
    int? CalculatedScore,
    string Title,
    string? Description,
    DateTimeOffset OpenedAt,
    DateTimeOffset? LastUpdated,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? SolvedAt,
    DateTimeOffset? DueDate,
    int EscalationLevel,
    bool IsEscalated,
    Guid RequesterUserId,
    Guid? AssignedUserId,
    Guid? AssignedGroupId,
    Guid? ItilCategoryId,
    Guid? SlaLevelId,
    Guid? AssetId,
    Guid? LocationId,
    TicketSource Source,
    TicketValidationStatus ValidationStatus,
    int? SatisfactionRating,
    string? SatisfactionComment,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record TicketListDto(
    Guid Id,
    TicketType Type,
    TicketStatus Status,
    TicketPriority Priority,
    string Title,
    Guid RequesterUserId,
    Guid? AssignedUserId,
    DateTimeOffset OpenedAt,
    DateTimeOffset? DueDate,
    bool IsEscalated);

public record CreateTicketRequest(
    TicketType Type,
    TicketPriority Priority,
    TicketImpact Impact,
    TicketUrgency Urgency,
    string Title,
    string? Description,
    Guid RequesterUserId,
    Guid? AssignedUserId,
    Guid? AssignedGroupId,
    Guid? ItilCategoryId,
    Guid? SlaLevelId,
    Guid? AssetId,
    Guid? LocationId,
    TicketSource Source);

public record UpdateTicketRequest(
    TicketType Type,
    TicketPriority Priority,
    TicketImpact Impact,
    TicketUrgency Urgency,
    string Title,
    string? Description,
    Guid? AssignedUserId,
    Guid? AssignedGroupId,
    Guid? ItilCategoryId,
    Guid? SlaLevelId,
    Guid? AssetId,
    Guid? LocationId);

public record TicketPagedResult(IReadOnlyList<TicketListDto> Items, int TotalCount, int Page, int PageSize);

// ---- Ticket Task DTOs ----
public record TicketTaskDto(
    Guid Id,
    Guid TicketId,
    Guid UserId,
    string Content,
    TicketTaskType Type,
    TicketStatus? PreviousStatus,
    TicketStatus? NewStatus,
    int? TimeSpentMinutes,
    bool IsPrivate,
    DateTimeOffset CreatedAt);

public record CreateTicketTaskRequest(
    string Content,
    TicketTaskType Type,
    int? TimeSpentMinutes,
    bool IsPrivate);

// ---- Ticket Template DTOs ----
public record TicketTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    TicketType DefaultType,
    TicketPriority DefaultPriority,
    TicketImpact DefaultImpact,
    TicketUrgency DefaultUrgency,
    string? TitleTemplate,
    string? DescriptionTemplate,
    Guid? DefaultCategoryId,
    Guid? DefaultAssignedUserId,
    Guid? DefaultAssignedGroupId,
    bool IsActive,
    int SortOrder);

public record CreateTicketTemplateRequest(
    string Name,
    string? Description,
    TicketType DefaultType,
    TicketPriority DefaultPriority,
    TicketImpact DefaultImpact,
    TicketUrgency DefaultUrgency,
    string? TitleTemplate,
    string? DescriptionTemplate,
    Guid? DefaultCategoryId,
    Guid? DefaultAssignedUserId,
    Guid? DefaultAssignedGroupId,
    int SortOrder = 0);

public record UpdateTicketTemplateRequest(
    string Name,
    string? Description,
    TicketType DefaultType,
    TicketPriority DefaultPriority,
    TicketImpact DefaultImpact,
    TicketUrgency DefaultUrgency,
    string? TitleTemplate,
    string? DescriptionTemplate,
    Guid? DefaultCategoryId,
    Guid? DefaultAssignedUserId,
    Guid? DefaultAssignedGroupId,
    bool IsActive,
    int SortOrder = 0);

// ---- Ticket Validation DTOs ----
public record TicketValidationDto(
    Guid Id,
    Guid TicketId,
    Guid UserId,
    TicketValidationItemStatus Status,
    string? Comment,
    DateTimeOffset? ValidatedAt,
    int StepNumber,
    bool IsMandatory);

public record SubmitValidationRequest(
    TicketValidationItemStatus Status,
    string? Comment);

// ---- Ticket Cost DTOs ----
public record TicketCostDto(
    Guid Id,
    Guid TicketId,
    TicketCostType CostType,
    string Description,
    decimal Amount,
    string Currency,
    Guid UserId,
    DateTimeOffset IncurredAt,
    string? Reference);

public record CreateTicketCostRequest(
    TicketCostType CostType,
    string Description,
    decimal Amount,
    string Currency,
    DateTimeOffset? IncurredAt,
    string? Reference);

// ---- Ticket History DTOs ----
public record TicketHistoryDto(
    Guid Id,
    Guid TicketId,
    Guid UserId,
    string FieldName,
    string? OldValue,
    string? NewValue,
    TicketHistoryAction Action,
    DateTimeOffset OccurredAt,
    string? Comment);
