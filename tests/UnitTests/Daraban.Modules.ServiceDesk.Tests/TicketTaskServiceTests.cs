using Daraban.Modules.ServiceDesk.Data.Entities;
using Daraban.Modules.ServiceDesk.Data.Repositories;
using Daraban.Modules.ServiceDesk.Services;
using Daraban.Modules.ServiceDesk.Services.Dtos;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.ServiceDesk.Tests;

/// <summary>
/// TicketTaskService: work logged against a ticket. The two behaviours worth pinning are that a
/// delete actually deletes (it used to call Update and report success while changing nothing) and
/// that logging work advances an Assigned ticket into InProgress.
/// </summary>
public class TicketTaskServiceTests
{
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly Mock<ITicketTaskRepository> _tasks = new(MockBehavior.Strict);
    private readonly Mock<ITicketRepository> _tickets = new(MockBehavior.Strict);

    private TicketTaskService CreateSut() => new(_tasks.Object, _tickets.Object);

    private static Ticket TicketWith(TicketStatus status) => new()
    {
        Id = Guid.CreateVersion7(),
        EntityId = Guid.CreateVersion7(),
        Status = status,
        Title = "Laptop will not boot",
        RequesterUserId = Guid.CreateVersion7(),
    };

    private static TicketTask TaskOf(Guid ticketId, Guid userId) => new()
    {
        Id = Guid.CreateVersion7(),
        TicketId = ticketId,
        UserId = userId,
        Content = "Ran diagnostics",
        Type = TicketTaskType.Action,
    };

    private static CreateTicketTaskRequest Request(
        TicketTaskType type = TicketTaskType.Comment,
        int? minutes = 30,
        bool isPrivate = false)
        => new("Ran diagnostics", type, minutes, isPrivate);

    // ---- Create ------------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Returns_NotFound_For_A_Missing_Ticket()
    {
        var ticketId = Guid.CreateVersion7();
        _tickets.Setup(r => r.GetByIdAsync(ticketId, It.IsAny<CancellationToken>())).ReturnsAsync((Ticket?)null);

        var result = await CreateSut().CreateAsync(ticketId, Request(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.NOT_FOUND", result.Error!.Code);
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task CreateAsync_Refuses_Terminal_Tickets(TicketStatus status)
    {
        var ticket = TicketWith(status);
        _tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().CreateAsync(ticket.Id, Request(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.TASK_BLOCKED", result.Error!.Code);
        _tasks.Verify(r => r.AddAsync(It.IsAny<TicketTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Records_The_Actor_As_Author()
    {
        var ticket = TicketWith(TicketStatus.InProgress);
        TicketTask? captured = null;

        _tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _tasks.Setup(r => r.AddAsync(It.IsAny<TicketTask>(), It.IsAny<CancellationToken>()))
            .Callback<TicketTask, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);
        _tasks.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(
            ticket.Id, Request(TicketTaskType.Action, 45, isPrivate: true), ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActorId, captured!.UserId);
        Assert.Equal(45, captured.TimeSpentMinutes);
        Assert.True(captured.IsPrivate);
        Assert.Equal(7, captured.Id.Version); // UUIDv7, matching every other module
    }

    [Fact]
    public async Task CreateAsync_Populates_Both_Statuses_On_A_StatusChange_Task()
    {
        var ticket = TicketWith(TicketStatus.InProgress);
        TicketTask? captured = null;

        _tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _tasks.Setup(r => r.AddAsync(It.IsAny<TicketTask>(), It.IsAny<CancellationToken>()))
            .Callback<TicketTask, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);
        _tasks.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().CreateAsync(ticket.Id, Request(TicketTaskType.StatusChange), ActorId);

        // Only PreviousStatus used to be set, leaving NewStatus permanently null -- a row claiming
        // a status change had happened without ever saying to what.
        Assert.Equal(TicketStatus.InProgress, captured!.PreviousStatus);
        Assert.Equal(TicketStatus.InProgress, captured.NewStatus);
    }

    [Theory]
    [InlineData(TicketTaskType.Action)]
    [InlineData(TicketTaskType.Comment)]
    public async Task CreateAsync_Advances_An_Assigned_Ticket_To_InProgress(TicketTaskType type)
    {
        var ticket = TicketWith(TicketStatus.Assigned);
        var history = new List<TicketHistory>();

        _tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _tickets.Setup(r => r.UpdateAsync(ticket, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _tickets.Setup(r => r.AddHistoryAsync(It.IsAny<TicketHistory>(), It.IsAny<CancellationToken>()))
            .Callback<TicketHistory, CancellationToken>((h, _) => history.Add(h))
            .Returns(Task.CompletedTask);
        _tasks.Setup(r => r.AddAsync(It.IsAny<TicketTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _tasks.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(ticket.Id, Request(type), ActorId);

        // Otherwise a ticket can accumulate hours of logged work while still claiming nobody has
        // started on it.
        Assert.True(result.IsSuccess);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);

        var entry = Assert.Single(history);
        Assert.Equal("Assigned", entry.OldValue);
        Assert.Equal("InProgress", entry.NewValue);
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingForUser)]
    [InlineData(TicketStatus.Solved)]
    public async Task CreateAsync_Leaves_Other_Statuses_Alone(TicketStatus status)
    {
        var ticket = TicketWith(status);

        _tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _tasks.Setup(r => r.AddAsync(It.IsAny<TicketTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _tasks.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().CreateAsync(ticket.Id, Request(), ActorId);

        // Only Assigned advances. A comment on a New ticket does not mean work has begun, and one
        // on a Solved ticket must not reopen it.
        Assert.Equal(status, ticket.Status);
        _tickets.Verify(r => r.UpdateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Does_Not_Advance_On_A_StatusChange_Task()
    {
        var ticket = TicketWith(TicketStatus.Assigned);

        _tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _tasks.Setup(r => r.AddAsync(It.IsAny<TicketTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _tasks.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().CreateAsync(ticket.Id, Request(TicketTaskType.StatusChange), ActorId);

        // A StatusChange task documents a transition somebody else performed; it must not perform
        // one of its own.
        Assert.Equal(TicketStatus.Assigned, ticket.Status);
    }

    // ---- Read --------------------------------------------------------------------------------

    [Fact]
    public async Task GetByTicketIdAsync_Returns_NotFound_For_A_Missing_Ticket()
    {
        var ticketId = Guid.CreateVersion7();
        _tickets.Setup(r => r.ExistsAsync(ticketId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateSut().GetByTicketIdAsync(ticketId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task GetByTicketIdAsync_Maps_Every_Task()
    {
        var ticketId = Guid.CreateVersion7();
        var tasks = new[] { TaskOf(ticketId, ActorId), TaskOf(ticketId, OtherUserId) };

        _tickets.Setup(r => r.ExistsAsync(ticketId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _tasks.Setup(r => r.GetByTicketIdAsync(ticketId, It.IsAny<CancellationToken>())).ReturnsAsync(tasks);

        var result = await CreateSut().GetByTicketIdAsync(ticketId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task GetByTicketIdAsync_Returns_An_Empty_List_For_A_Ticket_With_No_Tasks()
    {
        var ticketId = Guid.CreateVersion7();

        _tickets.Setup(r => r.ExistsAsync(ticketId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _tasks.Setup(r => r.GetByTicketIdAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TicketTask>());

        var result = await CreateSut().GetByTicketIdAsync(ticketId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    // ---- Delete ------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_Returns_NotFound_For_A_Missing_Task()
    {
        var id = Guid.CreateVersion7();
        _tasks.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((TicketTask?)null);

        var result = await CreateSut().DeleteAsync(id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET_TASK.NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task DeleteAsync_Refuses_To_Delete_Another_Users_Task()
    {
        var task = TaskOf(Guid.CreateVersion7(), OtherUserId);
        _tasks.Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var result = await CreateSut().DeleteAsync(task.Id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET_TASK.FORBIDDEN", result.Error!.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        _tasks.Verify(r => r.RemoveAsync(It.IsAny<TicketTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_Actually_Removes_The_Row()
    {
        var task = TaskOf(Guid.CreateVersion7(), ActorId);

        _tasks.Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(r => r.RemoveAsync(task, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _tasks.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().DeleteAsync(task.Id, ActorId);

        // This used to call UpdateAsync without changing anything: the method reported success and
        // the task stayed exactly where it was. TicketTask has no soft-delete columns, so removal
        // is the only honest implementation.
        Assert.True(result.IsSuccess);
        _tasks.Verify(r => r.RemoveAsync(task, It.IsAny<CancellationToken>()), Times.Once);
        _tasks.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
