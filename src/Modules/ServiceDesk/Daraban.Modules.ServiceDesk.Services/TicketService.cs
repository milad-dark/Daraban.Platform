using Daraban.Modules.ServiceDesk.Data.Entities;
using Daraban.Modules.ServiceDesk.Data.Repositories;
using Daraban.Modules.ServiceDesk.Services.Dtos;
using Daraban.Modules.ServiceDesk.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Contracts.ServiceDesk;

namespace Daraban.Modules.ServiceDesk.Services;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IEventPublisher _events;

    public TicketService(ITicketRepository ticketRepository, IEventPublisher events)
    {
        _ticketRepository = ticketRepository;
        _events = events;
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
        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);

        var (items, totalCount) = await _ticketRepository.GetPagedAsync(
            entityNodeId, type, status, priority, assignedUserId, assignedGroupId, search,
            normalizedPage, normalizedPageSize, ct);

        var dtos = items.Select(MapToListDto).ToList();
        return Result.Success(new TicketPagedResult(dtos, totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<TicketDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetWithDetailsAsync(id, ct);
        if (ticket is null)
            return Result.Failure<TicketDto>(NotFound());

        return Result.Success(MapToDto(ticket));
    }

    public async Task<Result<TicketDto>> CreateAsync(
        CreateTicketRequest request, Guid entityNodeId, Guid actorUserId, CancellationToken ct = default)
    {
        if (entityNodeId == Guid.Empty)
            return Result.Failure<TicketDto>(new Error(
                "TICKET.ENTITY_REQUIRED", "A tenant entity is required to create a ticket.", ErrorType.Validation));

        if (request.RequesterUserId == Guid.Empty)
            return Result.Failure<TicketDto>(new Error(
                "TICKET.REQUESTER_REQUIRED", "A requester is required.", ErrorType.Validation));

        var now = DateTimeOffset.UtcNow;
        var ticketId = Guid.CreateVersion7();

        // A ticket that arrives already assigned skips New and opens as Assigned, so its status
        // never contradicts the fact that somebody owns it.
        var hasAssignee = request.AssignedUserId is not null || request.AssignedGroupId is not null;

        var ticket = new Ticket
        {
            Id = ticketId,
            EntityId = entityNodeId,
            Type = request.Type,
            Status = hasAssignee ? TicketStatus.Assigned : TicketStatus.New,
            Priority = request.Priority,
            Impact = request.Impact,
            Urgency = request.Urgency,
            CalculatedScore = CalculateScore(request.Priority, request.Impact, request.Urgency),
            Title = request.Title,
            Description = HtmlInputSanitizer.Sanitize(request.Description),
            OpenedAt = now,
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
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _ticketRepository.AddAsync(ticket, ct);
        await _ticketRepository.AddHistoryAsync(TicketHistory.Record(
            ticketId, actorUserId, nameof(Ticket.Status),
            null, ticket.Status.ToString(), TicketHistoryAction.Create), ct);
        await _ticketRepository.SaveChangesAsync(ct);

        await _events.PublishAsync(new TicketCreatedEvent(
            ticket.Id, ticket.EntityId, ticket.RequesterUserId), ct);

        return Result.Success(MapToDto(ticket));
    }

    public async Task<Result<TicketDto>> UpdateAsync(
        Guid id, UpdateTicketRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, ct);
        if (ticket is null)
            return Result.Failure<TicketDto>(NotFound());

        if (TicketStatuses.IsTerminal(ticket.Status))
            return Result.Failure<TicketDto>(new Error(
                "TICKET.UPDATE_BLOCKED", "Cannot update closed or cancelled tickets.", ErrorType.BusinessRule));

        var now = DateTimeOffset.UtcNow;

        // Only genuine changes are audited -- writing a history row for every field on every save
        // would bury the two edits that mattered under forty that did not.
        if (ticket.Title != request.Title)
            await _ticketRepository.AddHistoryAsync(TicketHistory.Record(
                id, actorUserId, nameof(Ticket.Title), ticket.Title, request.Title,
                TicketHistoryAction.Update), ct);

        if (ticket.Priority != request.Priority)
            await _ticketRepository.AddHistoryAsync(TicketHistory.Record(
                id, actorUserId, nameof(Ticket.Priority),
                ticket.Priority.ToString(), request.Priority.ToString(),
                TicketHistoryAction.Update), ct);

        if (ticket.Type != request.Type)
            await _ticketRepository.AddHistoryAsync(TicketHistory.Record(
                id, actorUserId, nameof(Ticket.Type),
                ticket.Type.ToString(), request.Type.ToString(),
                TicketHistoryAction.Update), ct);

        ticket.Type = request.Type;
        ticket.Priority = request.Priority;
        ticket.Impact = request.Impact;
        ticket.Urgency = request.Urgency;
        ticket.CalculatedScore = CalculateScore(request.Priority, request.Impact, request.Urgency);
        ticket.Title = request.Title;
        ticket.Description = HtmlInputSanitizer.Sanitize(request.Description);
        ticket.AssignedUserId = request.AssignedUserId;
        ticket.AssignedGroupId = request.AssignedGroupId;
        ticket.ItilCategoryId = request.ItilCategoryId;
        ticket.SlaLevelId = request.SlaLevelId;
        ticket.AssetId = request.AssetId;
        ticket.LocationId = request.LocationId;
        ticket.LastUpdated = now;
        ticket.UpdatedAt = now;
        ticket.UpdatedById = actorUserId;

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _ticketRepository.SaveChangesAsync(ct);

        return Result.Success(MapToDto(ticket));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, ct);
        if (ticket is null)
            return Result.Failure(NotFound());

        // A ticket that has been worked carries time records and history worth keeping. Only an
        // untouched or abandoned one can be removed.
        if (ticket.Status is not (TicketStatus.New or TicketStatus.Cancelled))
            return Result.Failure(new Error(
                "TICKET.DELETE_BLOCKED", "Can only delete new or cancelled tickets.", ErrorType.BusinessRule));

        var now = DateTimeOffset.UtcNow;
        ticket.IsDeleted = true;
        ticket.DeletedAt = now;
        ticket.UpdatedAt = now;
        ticket.UpdatedById = actorUserId;

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _ticketRepository.AddHistoryAsync(TicketHistory.Record(
            id, actorUserId, nameof(Ticket.IsDeleted), "false", "true",
            TicketHistoryAction.Delete), ct);
        await _ticketRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<TicketDto>> ChangeStatusAsync(
        Guid id, TicketStatus newStatus, Guid actorUserId, string? reason, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, ct);
        if (ticket is null)
            return Result.Failure<TicketDto>(NotFound());

        if (ticket.Status == newStatus)
            return Result.Failure<TicketDto>(new Error(
                "TICKET.STATUS_UNCHANGED", $"Ticket is already {newStatus}.", ErrorType.BusinessRule));

        if (!IsValidStatusTransition(ticket.Status, newStatus))
            return Result.Failure<TicketDto>(new Error(
                "TICKET.INVALID_TRANSITION",
                $"Cannot transition from {ticket.Status} to {newStatus}.", ErrorType.BusinessRule));

        // Cancelling is irreversible, so it must say why -- same rule as retiring an asset.
        if (newStatus == TicketStatus.Cancelled && string.IsNullOrWhiteSpace(reason))
            return Result.Failure<TicketDto>(new Error(
                "TICKET.REASON_REQUIRED", "A reason is required when cancelling a ticket.", ErrorType.BusinessRule));

        var previousStatus = ApplyStatus(ticket, newStatus, actorUserId);

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _ticketRepository.AddHistoryAsync(TicketHistory.Record(
            id, actorUserId, nameof(Ticket.Status),
            previousStatus.ToString(), newStatus.ToString(),
            TicketHistoryAction.StatusChange, reason), ct);
        await _ticketRepository.SaveChangesAsync(ct);

        return Result.Success(MapToDto(ticket));
    }

    public async Task<Result<TicketDto>> AssignAsync(
        Guid id, Guid? assignedUserId, Guid? assignedGroupId, Guid actorUserId, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, ct);
        if (ticket is null)
            return Result.Failure<TicketDto>(NotFound());

        if (TicketStatuses.IsTerminal(ticket.Status))
            return Result.Failure<TicketDto>(new Error(
                "TICKET.ASSIGN_BLOCKED", "Cannot assign closed or cancelled tickets.", ErrorType.BusinessRule));

        // Assigning to nobody is not an assignment. Without this the ticket ended up in status
        // Assigned with no assignee, which reads as "someone owns this" while nobody does.
        if (assignedUserId is null && assignedGroupId is null)
            return Result.Failure<TicketDto>(new Error(
                "TICKET.ASSIGNEE_REQUIRED",
                "An assigned user or group is required.", ErrorType.Validation));

        var previousUser = ticket.AssignedUserId;
        var previousGroup = ticket.AssignedGroupId;
        var previousStatus = ticket.Status;
        var now = DateTimeOffset.UtcNow;

        ticket.AssignedUserId = assignedUserId;
        ticket.AssignedGroupId = assignedGroupId;

        // Only an unassigned ticket advances to Assigned. Reassigning work already in progress or
        // parked on a supplier must not drag the ticket backwards through the workflow -- that
        // regression was also an illegal move under IsValidStatusTransition, so the ticket could
        // reach a state ChangeStatusAsync would have refused.
        if (ticket.Status == TicketStatus.New)
            ticket.Status = TicketStatus.Assigned;

        ticket.LastUpdated = now;
        ticket.UpdatedAt = now;
        ticket.UpdatedById = actorUserId;

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _ticketRepository.AddHistoryAsync(TicketHistory.Record(
            id, actorUserId, nameof(Ticket.AssignedUserId),
            FormatAssignee(previousUser, previousGroup),
            FormatAssignee(assignedUserId, assignedGroupId),
            TicketHistoryAction.Assignment), ct);

        if (previousStatus != ticket.Status)
            await _ticketRepository.AddHistoryAsync(TicketHistory.Record(
                id, actorUserId, nameof(Ticket.Status),
                previousStatus.ToString(), ticket.Status.ToString(),
                TicketHistoryAction.StatusChange), ct);

        await _ticketRepository.SaveChangesAsync(ct);

        return Result.Success(MapToDto(ticket));
    }

    public async Task<Result<TicketDto>> EscalateAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, ct);
        if (ticket is null)
            return Result.Failure<TicketDto>(NotFound());

        // Escalation is a call for more help, which is meaningless once the ticket is done. The
        // other mutations all guard this; escalate did not.
        if (TicketStatuses.IsTerminal(ticket.Status))
            return Result.Failure<TicketDto>(new Error(
                "TICKET.ESCALATE_BLOCKED",
                "Cannot escalate closed or cancelled tickets.", ErrorType.BusinessRule));

        // Levels run 0 -> 1 -> 2. Previously an IsEscalated flag check blocked the second step, so
        // level 2 was unreachable even though the model documented it.
        if (ticket.EscalationLevel >= Ticket.MaxEscalationLevel)
            return Result.Failure<TicketDto>(new Error(
                "TICKET.MAX_ESCALATION_REACHED",
                $"Ticket is already at the maximum escalation level ({Ticket.MaxEscalationLevel}).",
                ErrorType.BusinessRule));

        var previousLevel = ticket.EscalationLevel;
        var now = DateTimeOffset.UtcNow;

        ticket.EscalationLevel = previousLevel + 1;
        ticket.IsEscalated = true;
        ticket.LastUpdated = now;
        ticket.UpdatedAt = now;
        ticket.UpdatedById = actorUserId;

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _ticketRepository.AddHistoryAsync(TicketHistory.Record(
            id, actorUserId, nameof(Ticket.EscalationLevel),
            previousLevel.ToString(), ticket.EscalationLevel.ToString(),
            TicketHistoryAction.Update), ct);
        await _ticketRepository.SaveChangesAsync(ct);

        return Result.Success(MapToDto(ticket));
    }

    public async Task<Result<TicketDto>> SolveAsync(
        Guid id, Guid actorUserId, string? solution, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, ct);
        if (ticket is null)
            return Result.Failure<TicketDto>(NotFound());

        if (ticket.Status == TicketStatus.Solved)
            return Result.Failure<TicketDto>(new Error(
                "TICKET.STATUS_UNCHANGED", "Ticket is already solved.", ErrorType.BusinessRule));

        // Solve is a status transition and goes through the same state machine as any other. It
        // used to bypass it entirely, which let a brand-new ticket jump straight to Solved -- a
        // move ChangeStatusAsync rejects.
        if (!IsValidStatusTransition(ticket.Status, TicketStatus.Solved))
            return Result.Failure<TicketDto>(new Error(
                "TICKET.SOLVE_BLOCKED",
                $"Cannot solve a ticket with status {ticket.Status}.", ErrorType.BusinessRule));

        // A resolution with no description is not a resolution anyone can learn from later.
        if (string.IsNullOrWhiteSpace(solution))
            return Result.Failure<TicketDto>(new Error(
                "TICKET.SOLUTION_REQUIRED",
                "A solution description is required when solving a ticket.", ErrorType.BusinessRule));

        var previousStatus = ApplyStatus(ticket, TicketStatus.Solved, actorUserId);

        // Persisted on the ticket rather than discarded -- Ticket.Solution exists for exactly this.
        ticket.Solution = HtmlInputSanitizer.Sanitize(solution).Trim();

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _ticketRepository.AddHistoryAsync(TicketHistory.Record(
            id, actorUserId, nameof(Ticket.Status),
            previousStatus.ToString(), TicketStatus.Solved.ToString(),
            TicketHistoryAction.StatusChange, ticket.Solution), ct);
        await _ticketRepository.SaveChangesAsync(ct);

        return Result.Success(MapToDto(ticket));
    }

    public async Task<Result<TicketDto>> CloseAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id, ct);
        if (ticket is null)
            return Result.Failure<TicketDto>(NotFound());

        if (ticket.Status != TicketStatus.Solved)
            return Result.Failure<TicketDto>(new Error(
                "TICKET.CLOSE_BLOCKED", "Can only close solved tickets.", ErrorType.BusinessRule));

        var previousStatus = ApplyStatus(ticket, TicketStatus.Closed, actorUserId);

        await _ticketRepository.UpdateAsync(ticket, ct);
        await _ticketRepository.AddHistoryAsync(TicketHistory.Record(
            id, actorUserId, nameof(Ticket.Status),
            previousStatus.ToString(), TicketStatus.Closed.ToString(),
            TicketHistoryAction.StatusChange), ct);
        await _ticketRepository.SaveChangesAsync(ct);

        return Result.Success(MapToDto(ticket));
    }

    public async Task<Result<IReadOnlyList<TicketHistoryDto>>> GetHistoryAsync(
        Guid id, CancellationToken ct = default)
    {
        if (!await _ticketRepository.ExistsAsync(id, ct))
            return Result.Failure<IReadOnlyList<TicketHistoryDto>>(NotFound());

        var history = await _ticketRepository.GetHistoryAsync(id, ct);
        var dtos = history.Select(MapToHistoryDto).ToList();
        return Result.Success<IReadOnlyList<TicketHistoryDto>>(dtos);
    }

    public async Task<Result<int>> GetOpenCountAsync(Guid entityNodeId, CancellationToken ct = default)
        // Counts every status in TicketStatuses.Open. Counting only New under-reported the queue
        // by excluding all the work actually in flight.
        => Result.Success(await _ticketRepository.CountOpenAsync(entityNodeId, ct));

    public async Task<Result<int>> GetOverdueCountAsync(Guid entityNodeId, CancellationToken ct = default)
        // Real query against DueDate instead of the hardcoded 0 this used to return. Tickets with
        // no DueDate are not counted -- nothing populates it yet, so this reports 0 until an SLA
        // engine sets due dates, but it will start reporting the truth the moment one does.
        => Result.Success(await _ticketRepository.CountOverdueAsync(entityNodeId, DateTimeOffset.UtcNow, ct));

    // ---- state machine + helpers -------------------------------------------------------------

    /// <summary>
    /// Applies a status change plus its timestamp side effects. Shared by ChangeStatus, Solve and
    /// Close so the SolvedAt/ClosedAt stamps cannot drift between the three paths.
    /// </summary>
    private static TicketStatus ApplyStatus(Ticket ticket, TicketStatus newStatus, Guid actorUserId)
    {
        var previousStatus = ticket.Status;
        var now = DateTimeOffset.UtcNow;

        ticket.Status = newStatus;
        ticket.LastUpdated = now;
        ticket.UpdatedAt = now;
        ticket.UpdatedById = actorUserId;

        switch (newStatus)
        {
            case TicketStatus.Solved:
                ticket.SolvedAt = now;
                break;

            case TicketStatus.Closed:
                ticket.ClosedAt = now;
                // Closing straight after a reopen-and-fix cycle still needs a solved timestamp,
                // otherwise time-to-resolution reports have a hole in them.
                ticket.SolvedAt ??= now;
                break;

            case TicketStatus.InProgress when previousStatus == TicketStatus.Solved:
                // Reopened: the earlier resolution no longer holds, so its timestamp must not
                // linger and make the ticket look resolved.
                ticket.SolvedAt = null;
                break;
        }

        return previousStatus;
    }

    /// <summary>
    /// GLPI-style priority score. Public so callers and tests share one definition rather than
    /// each re-deriving Priority x Impact x Urgency.
    /// </summary>
    internal static int CalculateScore(TicketPriority priority, TicketImpact impact, TicketUrgency urgency)
        => (int)priority * (int)impact * (int)urgency;

    internal static bool IsValidStatusTransition(TicketStatus current, TicketStatus next)
        => current switch
        {
            TicketStatus.New => next is TicketStatus.Assigned or TicketStatus.InProgress or TicketStatus.Cancelled,
            TicketStatus.Assigned => next is TicketStatus.InProgress or TicketStatus.WaitingForUser or TicketStatus.WaitingForSupplier or TicketStatus.Cancelled,
            TicketStatus.InProgress => next is TicketStatus.WaitingForUser or TicketStatus.WaitingForSupplier or TicketStatus.Solved or TicketStatus.Cancelled,
            TicketStatus.WaitingForUser => next is TicketStatus.InProgress or TicketStatus.Solved or TicketStatus.Cancelled,
            TicketStatus.WaitingForSupplier => next is TicketStatus.InProgress or TicketStatus.Solved or TicketStatus.Cancelled,
            TicketStatus.Solved => next is TicketStatus.Closed or TicketStatus.InProgress,
            // Terminal -- reopening means raising a new ticket, so the original keeps its SLA
            // clock and audit trail intact.
            TicketStatus.Closed => false,
            TicketStatus.Cancelled => false,
            _ => false,
        };

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
        => (page < 1 ? 1 : page, pageSize switch { < 1 => 20, > 200 => 200, _ => pageSize });

    private static string? FormatAssignee(Guid? userId, Guid? groupId)
        => (userId, groupId) switch
        {
            (not null, not null) => $"user:{userId};group:{groupId}",
            (not null, null) => $"user:{userId}",
            (null, not null) => $"group:{groupId}",
            _ => null,
        };

    private static Error NotFound()
        => new("TICKET.NOT_FOUND", "Ticket not found.", ErrorType.NotFound);

    // ---- mapping ------------------------------------------------------------------------------

    private static TicketDto MapToDto(Ticket ticket) => new(
        ticket.Id,
        ticket.EntityId,
        ticket.Type,
        ticket.Status,
        ticket.Priority,
        ticket.Impact,
        ticket.Urgency,
        ticket.CalculatedScore,
        ticket.Title,
        ticket.Description,
        ticket.Solution,
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

    private static TicketHistoryDto MapToHistoryDto(TicketHistory h) => new(
        h.Id, h.TicketId, h.UserId, h.FieldName, h.OldValue, h.NewValue, h.Action, h.OccurredAt, h.Comment);
}
