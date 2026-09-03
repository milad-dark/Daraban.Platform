using Daraban.Modules.ServiceDesk.Data.Entities;
using Daraban.Modules.ServiceDesk.Data.Repositories;
using Daraban.Modules.ServiceDesk.Services.Dtos;
using Daraban.Modules.ServiceDesk.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.ServiceDesk.Services;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;

    public TicketService(ITicketRepository ticketRepository)
    {
        _ticketRepository = ticketRepository;
    }

    public async Task<Result<TicketPagedResult>> GetPagedAsync(
        Guid entityNodeId,
        TicketType? type,
        TicketStatus? status,
        TicketPriority? priority,
        Guid? assignedUserId,
        Guid? assignedGroupId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _ticketRepository.GetPagedAsync(
            entityNodeId, type, status, priority, assignedUserId, assignedGroupId, search, page, pageSize, ct);

        var dtos = items.Select(MapToListDto).ToList();
        return Result<TicketPagedResult>.Success(new TicketPagedResult(dtos, totalCount, page, pageSize));
    }

    public async Task<Result<TicketDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetWithDetailsAsync(id, ct);
        if (ticket is null)
            return Result.Failure<TicketDto>(new Error("TICKET.NOT_FOUND", "Ticket not found.", ErrorType.NotFound));

        return Result<TicketDto>.Success(MapToDto(ticket));
    }

    public async Task<Result<TicketDto>> CreateAsync(CreateTicketRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        // Calculate score from Priority * Impact * Urgency
        var calculatedScore = (int)request.Priority * (int)request.Impact * (int)request.Urgency;

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            EntityId = actorUserId, // TODO: Get from context
            Type = request.Type,
            Status = TicketStatus.New,
            Priority = request.Priority,
            Impact = request.Impact,
            Urgency = request.Urgency,
            CalculatedScore = calculatedScore,
            Title = request.Title,
            Description = request.Description,
            OpenedAt = DateTimeOffset.UtcNow,
            RequesterUserId = request.RequesterUserId,
            AssignedUserId = request.AssignedUserId,
            AssignedGroupId = request.AssignedGroupId,
            ItilCategoryId = request.ItilCategoryId,
            SlaLevelId = request.SlaLevelId,
            AssetId = request.AssetId,
            LocationId = request.LocationId,
            Source = request.Source,
            CreatedById = actorUserId,
            UpdatedById = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _ticketRepository.AddAsync(ticket, ct);
        await _ticketRepository.SaveChangesAsync(ct);

        return Result<TicketDto>.Success(MapToDto(ticket));
    }

    public async Task<Result<TicketDto>> UpdateAsync(Guid id, UpdateTicketRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, ct);
        if (ticket is null)
            return Result.Failure<TicketDto>(new Error("TICKET.NOT_FOUND", "Ticket not found.", ErrorType.NotFound));

        // Only allow updates on certain statuses
        if (ticket.Status == TicketStatus.Closed || ticket.Status == TicketStatus.Cancelled)
            return Result.Failure<TicketDto>(new Error("TICKET.UPDATE_BLOCKED", "Cannot update closed or cancelled tickets.", ErrorType.BusinessRule));

        // Calculate score from Priority * Impact * Urgency
        var calculatedScore = (int)request.Priority * (int)request.Impact * (int)request.Urgency;

        ticket.Type = request.Type;
        ticket.Priority = request.Priority;
        ticket.Impact = request.Impact;
        ticket.Urgency = request.Urgency;
        ticket.CalculatedScore = calculatedScore;
        ticket.Title = request.Title;
        ticket.Description = request.Description;
        ticket.AssignedUserId = request.AssignedUserId;
        ticket.AssignedGroupId = request.AssignedGroupId;
        ticket.ItilCategoryId = request.ItilCategoryId;
        ticket.SlaLevelId = request.SlaLevelId;
        ticket.AssetId = request.AssetId;
        ticket.LocationId = request.LocationId;
        ticket.LastUpdated = DateTimeOffset.UtcNow;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        ticket.UpdatedById = actorUserId;

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _ticketRepository.SaveChangesAsync(ct);

        return Result<TicketDto>.Success(MapToDto(ticket));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, ct);
        if (ticket is null)
            return Result.Failure(new Error("TICKET.NOT_FOUND", "Ticket not found.", ErrorType.NotFound));

        // Only allow deletion on certain statuses
        if (ticket.Status != TicketStatus.New && ticket.Status != TicketStatus.Cancelled)
            return Result.Failure(new Error("TICKET.DELETE_BLOCKED", "Can only delete new or cancelled tickets.", ErrorType.BusinessRule));

        // Soft delete
        ticket.IsDeleted = true;
        ticket.DeletedAt = DateTimeOffset.UtcNow;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        ticket.UpdatedById = actorUserId;

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _ticketRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<TicketDto>> ChangeStatusAsync(Guid id, TicketStatus newStatus, Guid actorUserId, string? reason, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, ct);
        if (ticket is null)
            return Result.Failure<TicketDto>(new Error("TICKET.NOT_FOUND", "Ticket not found.", ErrorType.NotFound));

        // Validate status transition
        var isValidTransition = IsValidStatusTransition(ticket.Status, newStatus);
        if (!isValidTransition)
            return Result.Failure<TicketDto>(new Error("TICKET.INVALID_TRANSITION", $"Cannot transition from {ticket.Status} to {newStatus}.", ErrorType.BusinessRule));

        var previousStatus = ticket.Status;
        ticket.Status = newStatus;
        ticket.LastUpdated = DateTimeOffset.UtcNow;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        ticket.UpdatedById = actorUserId;

        // Set timestamps based on status
        if (newStatus == TicketStatus.Solved)
            ticket.SolvedAt = DateTimeOffset.UtcNow;
        else if (newStatus == TicketStatus.Closed)
            ticket.ClosedAt = DateTimeOffset.UtcNow;

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _ticketRepository.SaveChangesAsync(ct);

        return Result<TicketDto>.Success(MapToDto(ticket));
    }

    public async Task<Result<TicketDto>> AssignAsync(Guid id, Guid? assignedUserId, Guid? assignedGroupId, Guid actorUserId, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, ct);
        if (ticket is null)
            return Result.Failure<TicketDto>(new Error("TICKET.NOT_FOUND", "Ticket not found.", ErrorType.NotFound));

        if (ticket.Status == TicketStatus.Closed || ticket.Status == TicketStatus.Cancelled)
            return Result.Failure<TicketDto>(new Error("TICKET.ASSIGN_BLOCKED", "Cannot assign closed or cancelled tickets.", ErrorType.BusinessRule));

        ticket.AssignedUserId = assignedUserId;
        ticket.AssignedGroupId = assignedGroupId;
        ticket.Status = TicketStatus.Assigned;
        ticket.LastUpdated = DateTimeOffset.UtcNow;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        ticket.UpdatedById = actorUserId;

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _ticketRepository.SaveChangesAsync(ct);

        return Result<TicketDto>.Success(MapToDto(ticket));
    }

    public async Task<Result<TicketDto>> EscalateAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, ct);
        if (ticket is null)
            return Result.Failure<TicketDto>(new Error("TICKET.NOT_FOUND", "Ticket not found.", ErrorType.NotFound));

        if (ticket.IsEscalated)
            return Result.Failure<TicketDto>(new Error("TICKET.ALREADY_ESCALATED", "Ticket is already escalated.", ErrorType.BusinessRule));

        ticket.IsEscalated = true;
        ticket.EscalationLevel++;
        ticket.LastUpdated = DateTimeOffset.UtcNow;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        ticket.UpdatedById = actorUserId;

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _ticketRepository.SaveChangesAsync(ct);

        return Result<TicketDto>.Success(MapToDto(ticket));
    }

    public async Task<Result<TicketDto>> SolveAsync(Guid id, Guid actorUserId, string? solution, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, ct);
        if (ticket is null)
            return Result.Failure<TicketDto>(new Error("TICKET.NOT_FOUND", "Ticket not found.", ErrorType.NotFound));

        if (ticket.Status == TicketStatus.Closed || ticket.Status == TicketStatus.Cancelled)
            return Result.Failure<TicketDto>(new Error("TICKET.SOLVE_BLOCKED", "Cannot solve closed or cancelled tickets.", ErrorType.BusinessRule));

        ticket.Status = TicketStatus.Solved;
        ticket.SolvedAt = DateTimeOffset.UtcNow;
        ticket.LastUpdated = DateTimeOffset.UtcNow;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        ticket.UpdatedById = actorUserId;

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _ticketRepository.SaveChangesAsync(ct);

        return Result<TicketDto>.Success(MapToDto(ticket));
    }

    public async Task<Result<TicketDto>> CloseAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, ct);
        if (ticket is null)
            return Result.Failure<TicketDto>(new Error("TICKET.NOT_FOUND", "Ticket not found.", ErrorType.NotFound));

        if (ticket.Status != TicketStatus.Solved)
            return Result.Failure<TicketDto>(new Error("TICKET.CLOSE_BLOCKED", "Can only close solved tickets.", ErrorType.BusinessRule));

        ticket.Status = TicketStatus.Closed;
        ticket.ClosedAt = DateTimeOffset.UtcNow;
        ticket.LastUpdated = DateTimeOffset.UtcNow;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        ticket.UpdatedById = actorUserId;

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _ticketRepository.SaveChangesAsync(ct);

        return Result<TicketDto>.Success(MapToDto(ticket));
    }

    public async Task<Result<int>> GetOpenCountAsync(Guid entityNodeId, CancellationToken ct = default)
    {
        var (items, totalCount) = await _ticketRepository.GetPagedAsync(
            entityNodeId, null, TicketStatus.New, null, null, null, null, 1, 1, ct);
        return Result<int>.Success(totalCount);
    }

    public async Task<Result<int>> GetOverdueCountAsync(Guid entityNodeId, CancellationToken ct = default)
    {
        // TODO: Implement overdue count based on SLA
        return Result<int>.Success(0);
    }

    private static bool IsValidStatusTransition(TicketStatus current, TicketStatus next)
    {
        return current switch
        {
            TicketStatus.New => next is TicketStatus.Assigned or TicketStatus.InProgress or TicketStatus.Cancelled,
            TicketStatus.Assigned => next is TicketStatus.InProgress or TicketStatus.WaitingForUser or TicketStatus.WaitingForSupplier or TicketStatus.Cancelled,
            TicketStatus.InProgress => next is TicketStatus.WaitingForUser or TicketStatus.WaitingForSupplier or TicketStatus.Solved or TicketStatus.Cancelled,
            TicketStatus.WaitingForUser => next is TicketStatus.InProgress or TicketStatus.Solved or TicketStatus.Cancelled,
            TicketStatus.WaitingForSupplier => next is TicketStatus.InProgress or TicketStatus.Solved or TicketStatus.Cancelled,
            TicketStatus.Solved => next is TicketStatus.Closed or TicketStatus.InProgress,
            TicketStatus.Closed => false,
            TicketStatus.Cancelled => false,
            _ => false
        };
    }

    private static TicketDto MapToDto(Ticket ticket) => new(
        ticket.Id,
        ticket.Type,
        ticket.Status,
        ticket.Priority,
        ticket.Impact,
        ticket.Urgency,
        ticket.CalculatedScore,
        ticket.Title,
        ticket.Description,
        ticket.OpenedAt,
        ticket.LastUpdated,
        ticket.ClosedAt,
        ticket.SolvedAt,
        ticket.DueDate,
        ticket.EscalationLevel,
        ticket.IsEscalated,
        ticket.RequesterUserId,
        ticket.AssignedUserId,
        ticket.AssignedGroupId,
        ticket.ItilCategoryId,
        ticket.SlaLevelId,
        ticket.AssetId,
        ticket.LocationId,
        ticket.Source,
        ticket.ValidationStatus,
        ticket.SatisfactionRating,
        ticket.SatisfactionComment,
        ticket.CreatedAt,
        ticket.UpdatedAt);

    private static TicketListDto MapToListDto(Ticket ticket) => new(
        ticket.Id,
        ticket.Type,
        ticket.Status,
        ticket.Priority,
        ticket.Title,
        ticket.RequesterUserId,
        ticket.AssignedUserId,
        ticket.OpenedAt,
        ticket.DueDate,
        ticket.IsEscalated);
}
