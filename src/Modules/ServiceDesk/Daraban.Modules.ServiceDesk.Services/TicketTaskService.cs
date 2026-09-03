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

    public async Task<Result<IReadOnlyList<TicketTaskDto>>> GetByTicketIdAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticketExists = await _ticketRepository.ExistsAsync(ticketId, ct);
        if (!ticketExists)
            return Result.Failure<IReadOnlyList<TicketTaskDto>>(new Error("TICKET.NOT_FOUND", "Ticket not found.", ErrorType.NotFound));

        var tasks = await _ticketTaskRepository.GetByTicketIdAsync(ticketId, ct);
        IReadOnlyList<TicketTaskDto> dtos = tasks.Select(MapToDto).ToList();
        return Result<IReadOnlyList<TicketTaskDto>>.Success(dtos);
    }

    public async Task<Result<TicketTaskDto>> CreateAsync(Guid ticketId, CreateTicketTaskRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId, ct);
        if (ticket is null)
            return Result.Failure<TicketTaskDto>(new Error("TICKET.NOT_FOUND", "Ticket not found.", ErrorType.NotFound));

        if (ticket.Status == TicketStatus.Closed || ticket.Status == TicketStatus.Cancelled)
            return Result.Failure<TicketTaskDto>(new Error("TICKET.TASK_BLOCKED", "Cannot add tasks to closed or cancelled tickets.", ErrorType.BusinessRule));

        var task = new TicketTask
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            UserId = actorUserId,
            Content = request.Content,
            Type = request.Type,
            TimeSpentMinutes = request.TimeSpentMinutes,
            IsPrivate = request.IsPrivate,
            CreatedById = actorUserId,
            UpdatedById = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // If this is a status change task, capture the current status
        if (request.Type == TicketTaskType.StatusChange)
        {
            task.PreviousStatus = ticket.Status;
        }

        await _ticketTaskRepository.AddAsync(task, ct);
        await _ticketTaskRepository.SaveChangesAsync(ct);

        return Result<TicketTaskDto>.Success(MapToDto(task));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var task = await _ticketTaskRepository.GetByIdAsync(id, ct);
        if (task is null)
            return Result.Failure(new Error("TICKET_TASK.NOT_FOUND", "Ticket task not found.", ErrorType.NotFound));

        // Only allow deletion if the task was created by the same user
        if (task.UserId != actorUserId)
            return Result.Failure(new Error("TICKET_TASK.FORBIDDEN", "You can only delete your own tasks.", ErrorType.Forbidden));

        await _ticketTaskRepository.UpdateAsync(task, ct);
        await _ticketTaskRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

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
