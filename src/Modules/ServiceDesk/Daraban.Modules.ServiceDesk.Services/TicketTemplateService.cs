using Daraban.Modules.ServiceDesk.Data.Entities;
using Daraban.Modules.ServiceDesk.Data.Repositories;
using Daraban.Modules.ServiceDesk.Services.Dtos;
using Daraban.Modules.ServiceDesk.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.ServiceDesk.Services;

public class TicketTemplateService : ITicketTemplateService
{
    private readonly ITicketTemplateRepository _ticketTemplateRepository;

    public TicketTemplateService(ITicketTemplateRepository ticketTemplateRepository)
    {
        _ticketTemplateRepository = ticketTemplateRepository;
    }

    public async Task<Result<IReadOnlyList<TicketTemplateDto>>> GetAllAsync(Guid entityNodeId, CancellationToken ct = default)
    {
        var templates = await _ticketTemplateRepository.GetAllAsync(entityNodeId, ct);
        IReadOnlyList<TicketTemplateDto> dtos = templates.Select(MapToDto).ToList();
        return Result<IReadOnlyList<TicketTemplateDto>>.Success(dtos);
    }

    public async Task<Result<TicketTemplateDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _ticketTemplateRepository.GetByIdAsync(id, ct);
        if (template is null)
            return Result.Failure<TicketTemplateDto>(new Error("TICKET_TEMPLATE.NOT_FOUND", "Ticket template not found.", ErrorType.NotFound));

        return Result<TicketTemplateDto>.Success(MapToDto(template));
    }

    public async Task<Result<TicketTemplateDto>> CreateAsync(CreateTicketTemplateRequest request, Guid entityNodeId, Guid actorUserId, CancellationToken ct = default)
    {
        // Check for duplicate name
        var nameExists = await _ticketTemplateRepository.NameExistsAsync(request.Name, entityNodeId, null, ct);
        if (nameExists)
            return Result.Failure<TicketTemplateDto>(new Error("TICKET_TEMPLATE.NAME_EXISTS", "A template with this name already exists.", ErrorType.Conflict));

        var template = new TicketTemplate
        {
            Id = Guid.NewGuid(),
            EntityId = entityNodeId,
            Name = request.Name,
            Description = request.Description,
            DefaultType = request.DefaultType,
            DefaultPriority = request.DefaultPriority,
            DefaultImpact = request.DefaultImpact,
            DefaultUrgency = request.DefaultUrgency,
            TitleTemplate = request.TitleTemplate,
            DescriptionTemplate = request.DescriptionTemplate,
            DefaultCategoryId = request.DefaultCategoryId,
            DefaultAssignedUserId = request.DefaultAssignedUserId,
            DefaultAssignedGroupId = request.DefaultAssignedGroupId,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedById = actorUserId,
            UpdatedById = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _ticketTemplateRepository.AddAsync(template, ct);
        await _ticketTemplateRepository.SaveChangesAsync(ct);

        return Result<TicketTemplateDto>.Success(MapToDto(template));
    }

    public async Task<Result<TicketTemplateDto>> UpdateAsync(Guid id, UpdateTicketTemplateRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var template = await _ticketTemplateRepository.GetByIdAsync(id, ct);
        if (template is null)
            return Result.Failure<TicketTemplateDto>(new Error("TICKET_TEMPLATE.NOT_FOUND", "Ticket template not found.", ErrorType.NotFound));

        // Check for duplicate name (excluding current template)
        var nameExists = await _ticketTemplateRepository.NameExistsAsync(request.Name, template.EntityId, id, ct);
        if (nameExists)
            return Result.Failure<TicketTemplateDto>(new Error("TICKET_TEMPLATE.NAME_EXISTS", "A template with this name already exists.", ErrorType.Conflict));

        template.Name = request.Name;
        template.Description = request.Description;
        template.DefaultType = request.DefaultType;
        template.DefaultPriority = request.DefaultPriority;
        template.DefaultImpact = request.DefaultImpact;
        template.DefaultUrgency = request.DefaultUrgency;
        template.TitleTemplate = request.TitleTemplate;
        template.DescriptionTemplate = request.DescriptionTemplate;
        template.DefaultCategoryId = request.DefaultCategoryId;
        template.DefaultAssignedUserId = request.DefaultAssignedUserId;
        template.DefaultAssignedGroupId = request.DefaultAssignedGroupId;
        template.IsActive = request.IsActive;
        template.SortOrder = request.SortOrder;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        template.UpdatedById = actorUserId;

        await _ticketTemplateRepository.UpdateAsync(template, ct);
        await _ticketTemplateRepository.SaveChangesAsync(ct);

        return Result<TicketTemplateDto>.Success(MapToDto(template));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var template = await _ticketTemplateRepository.GetByIdAsync(id, ct);
        if (template is null)
            return Result.Failure(new Error("TICKET_TEMPLATE.NOT_FOUND", "Ticket template not found.", ErrorType.NotFound));

        // Soft delete
        template.IsDeleted = true;
        template.DeletedAt = DateTimeOffset.UtcNow;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        template.UpdatedById = actorUserId;

        await _ticketTemplateRepository.UpdateAsync(template, ct);
        await _ticketTemplateRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static TicketTemplateDto MapToDto(TicketTemplate template) => new(
        template.Id,
        template.Name,
        template.Description,
        template.DefaultType,
        template.DefaultPriority,
        template.DefaultImpact,
        template.DefaultUrgency,
        template.TitleTemplate,
        template.DescriptionTemplate,
        template.DefaultCategoryId,
        template.DefaultAssignedUserId,
        template.DefaultAssignedGroupId,
        template.IsActive,
        template.SortOrder);
}
