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

    public async Task<Result<IReadOnlyList<TicketTemplateDto>>> GetAllAsync(
        Guid entityNodeId, bool includeInactive = false, CancellationToken ct = default)
    {
        var templates = await _ticketTemplateRepository.GetAllAsync(entityNodeId, includeInactive, ct);
        var dtos = templates.Select(MapToDto).ToList();
        return Result.Success<IReadOnlyList<TicketTemplateDto>>(dtos);
    }

    public async Task<Result<TicketTemplateDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _ticketTemplateRepository.GetByIdAsync(id, ct);
        if (template is null)
            return Result.Failure<TicketTemplateDto>(NotFound());

        return Result.Success(MapToDto(template));
    }

    public async Task<Result<TicketTemplateDto>> CreateAsync(
        CreateTicketTemplateRequest request, Guid entityNodeId, Guid actorUserId, CancellationToken ct = default)
    {
        if (entityNodeId == Guid.Empty)
            return Result.Failure<TicketTemplateDto>(new Error(
                "TICKET_TEMPLATE.ENTITY_REQUIRED",
                "A tenant entity is required to create a template.", ErrorType.Validation));

        var nameExists = await _ticketTemplateRepository.NameExistsAsync(request.Name, entityNodeId, null, ct);
        if (nameExists)
            return Result.Failure<TicketTemplateDto>(NameExists(request.Name));

        var now = DateTimeOffset.UtcNow;
        var template = new TicketTemplate
        {
            // UUIDv7, matching every other module -- Guid.NewGuid() is v4 and produces random
            // index inserts on a table clustered by id.
            Id = Guid.CreateVersion7(),
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
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _ticketTemplateRepository.AddAsync(template, ct);
        await _ticketTemplateRepository.SaveChangesAsync(ct);

        return Result.Success(MapToDto(template));
    }

    public async Task<Result<TicketTemplateDto>> UpdateAsync(
        Guid id, UpdateTicketTemplateRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var template = await _ticketTemplateRepository.GetByIdAsync(id, ct);
        if (template is null)
            return Result.Failure<TicketTemplateDto>(NotFound());

        var nameExists = await _ticketTemplateRepository.NameExistsAsync(request.Name, template.EntityId, id, ct);
        if (nameExists)
            return Result.Failure<TicketTemplateDto>(NameExists(request.Name));

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

        return Result.Success(MapToDto(template));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var template = await _ticketTemplateRepository.GetByIdAsync(id, ct);
        if (template is null)
            return Result.Failure(NotFound());

        if (template.IsDeleted)
            return Result.Failure(new Error(
                "TICKET_TEMPLATE.ALREADY_DELETED", "Template is already deleted.", ErrorType.BusinessRule));

        var now = DateTimeOffset.UtcNow;
        template.IsDeleted = true;
        template.DeletedAt = now;
        // Deactivated as well as deleted: the soft-delete query filter hides the row, but leaving
        // IsActive true would make a restored template silently reappear in pickers.
        template.IsActive = false;
        template.UpdatedAt = now;
        template.UpdatedById = actorUserId;

        await _ticketTemplateRepository.UpdateAsync(template, ct);
        await _ticketTemplateRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static Error NotFound()
        => new("TICKET_TEMPLATE.NOT_FOUND", "Ticket template not found.", ErrorType.NotFound);

    private static Error NameExists(string name)
        => new("TICKET_TEMPLATE.NAME_EXISTS", $"A template named '{name}' already exists.", ErrorType.Conflict);

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
