using Daraban.Modules.ServiceDesk.Data.Entities;
using Daraban.Modules.ServiceDesk.Data.Repositories;
using Daraban.Modules.ServiceDesk.Services.Dtos;
using Daraban.Modules.ServiceDesk.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.ServiceDesk.Services;

public class TicketTaskService : ITicketTaskService
{
    private readonly ITicketTaskRepository _ticketTaskRepository;
    private readonly ITicketRepository _ticketRepository;

    public TicketTaskService(ITicketTaskRepository ticketTaskRepository, ITicketRepository ticketRepository)
    {
        _ticketTaskRepository = ticketTaskRepository;
        _ticketRepository = ticketRepository;
    }

    public async Task<Result<IReadOnlyList<TicketTaskDto>>> GetByTicketIdAsync(
        Guid ticketId, CancellationToken ct = default)
    {
        if (!await _ticketRepository.ExistsAsync(ticketId, ct))
            return Result.Failure<IReadOnlyList<TicketTaskDto>>(TicketNotFound<IReadOnlyList<TicketTaskDto>>());

        var tasks = await _ticketTaskRepository.GetByTicketIdAsync(ticketId, ct);
        var dtos = tasks.Select(MapToDto).ToList();
        return Result.Success<IReadOnlyList<TicketTaskDto>>(dtos);
    }

    public async Task<Result<TicketTaskDto>> CreateAsync(
        Guid ticketId, CreateTicketTaskRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId, ct);
        if (ticket is null)
            return Result.Failure<TicketTaskDto>(new Error(
                "TICKET.NOT_FOUND", "Ticket not found.", ErrorType.NotFound));

        if (TicketStatuses.IsTerminal(ticket.Status))
            return Result.Failure<TicketTaskDto>(new Error(
                "TICKET.TASK_BLOCKED",
                "Cannot add tasks to closed or cancelled tickets.", ErrorType.BusinessRule));

        var now = DateTimeOffset.UtcNow;
        var task = new TicketTask
        {
            Id = Guid.CreateVersion7(),
            TicketId = ticketId,
            UserId = actorUserId,
            Content = HtmlInputSanitizer.Sanitize(request.Content),
            Type = request.Type,
            TimeSpentMinutes = request.TimeSpentMinutes,
            IsPrivate = request.IsPrivate,
            CreatedById = actorUserId,
            UpdatedById = actorUserId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // A StatusChange task records the status it was written against on both sides. Capturing
        // only PreviousStatus left NewStatus permanently null, so the row claimed a status change
        // had happened without ever saying to what.
        if (request.Type == TicketTaskType.StatusChange)
        {
            task.PreviousStatus = ticket.Status;
            task.NewStatus = ticket.Status;
        }

        await _ticketTaskRepository.AddAsync(task, ct);

        // Logging work moves a ticket that was merely assigned into InProgress -- otherwise a
        // ticket can accumulate hours of recorded work while still claiming nobody has started.
        if (ticket.Status == TicketStatus.Assigned && request.Type is TicketTaskType.Action or TicketTaskType.Comment)
        {
            ticket.Status = TicketStatus.InProgress;
            ticket.LastUpdated = now;
            ticket.UpdatedAt = now;
            ticket.UpdatedById = actorUserId;

            await _ticketRepository.UpdateAsync(ticket, ct);
            await _ticketRepository.AddHistoryAsync(TicketHistory.Record(
                ticketId, actorUserId, nameof(Ticket.Status),
                TicketStatus.Assigned.ToString(), TicketStatus.InProgress.ToString(),
                TicketHistoryAction.StatusChange), ct);
        }

        await _ticketTaskRepository.SaveChangesAsync(ct);

        return Result.Success(MapToDto(task));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var task = await _ticketTaskRepository.GetByIdAsync(id, ct);
        if (task is null)
            return Result.Failure(new Error(
                "TICKET_TASK.NOT_FOUND", "Ticket task not found.", ErrorType.NotFound));

        if (task.UserId != actorUserId)
            return Result.Failure(new Error(
                "TICKET_TASK.FORBIDDEN", "You can only delete your own tasks.", ErrorType.Forbidden));

        // This used to call UpdateAsync without changing anything -- the method reported success
        // and the task stayed exactly where it was. TicketTask derives from BaseEntity and has no
        // soft-delete columns, so removal is the only honest implementation of "delete".
        await _ticketTaskRepository.RemoveAsync(task, ct);
        await _ticketTaskRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static Error TicketNotFound<T>()
        => new("TICKET.NOT_FOUND", "Ticket not found.", ErrorType.NotFound);

    private static TicketTaskDto MapToDto(TicketTask task) => new(
        task.Id,
        task.TicketId,
        task.UserId,
        task.Content,
        task.Type,
        task.PreviousStatus,
        task.NewStatus,
        task.TimeSpentMinutes,
        task.IsPrivate,
        task.CreatedAt);
}
