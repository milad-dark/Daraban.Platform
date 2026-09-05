using Daraban.Modules.ServiceDesk.Data.Entities;
using Daraban.Modules.ServiceDesk.Services;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.ServiceDesk.Tests;

/// <summary>
/// The ITIL workflow: ChangeStatus, Assign, Escalate, Solve, Close, Delete. Every transition is
/// asserted in both directions -- what is permitted and what must be refused -- because this
/// state machine is the only thing keeping a finished ticket finished.
/// </summary>
public class TicketServiceWorkflowTests : TicketServiceTestBase
{
    // ---- ChangeStatus ------------------------------------------------------------------------

    [Theory]
    [InlineData(TicketStatus.New, TicketStatus.Assigned)]
    [InlineData(TicketStatus.New, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Assigned, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Assigned, TicketStatus.WaitingForUser)]
    [InlineData(TicketStatus.Assigned, TicketStatus.WaitingForSupplier)]
    [InlineData(TicketStatus.InProgress, TicketStatus.WaitingForUser)]
    [InlineData(TicketStatus.InProgress, TicketStatus.WaitingForSupplier)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Solved)]
    [InlineData(TicketStatus.WaitingForUser, TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingForUser, TicketStatus.Solved)]
    [InlineData(TicketStatus.WaitingForSupplier, TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingForSupplier, TicketStatus.Solved)]
    [InlineData(TicketStatus.Solved, TicketStatus.Closed)]
    [InlineData(TicketStatus.Solved, TicketStatus.InProgress)]
    public async Task ChangeStatusAsync_Allows_The_Documented_Transitions(
        TicketStatus from, TicketStatus to)
    {
        var ticket = TicketWith(from);
        ArrangeMutation(ticket);

        var result = await CreateSut().ChangeStatusAsync(ticket.Id, to, ActorId, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(to, ticket.Status);
    }

    [Theory]
    [InlineData(TicketStatus.Closed, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Closed, TicketStatus.New)]
    [InlineData(TicketStatus.Closed, TicketStatus.Solved)]
    [InlineData(TicketStatus.Cancelled, TicketStatus.New)]
    [InlineData(TicketStatus.Cancelled, TicketStatus.InProgress)]
    public async Task ChangeStatusAsync_Treats_Closed_And_Cancelled_As_Terminal(
        TicketStatus from, TicketStatus to)
    {
        var ticket = TicketWith(from);
        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().ChangeStatusAsync(ticket.Id, to, ActorId, "reason");

        // Reopening a finished ticket must mean raising a new one, so the original keeps its SLA
        // clock and audit trail.
        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.INVALID_TRANSITION", result.Error!.Code);
        Assert.Equal(from, ticket.Status);
    }

    [Theory]
    [InlineData(TicketStatus.New, TicketStatus.Solved)]
    [InlineData(TicketStatus.New, TicketStatus.Closed)]
    [InlineData(TicketStatus.New, TicketStatus.WaitingForUser)]
    [InlineData(TicketStatus.Assigned, TicketStatus.Solved)]
    [InlineData(TicketStatus.Assigned, TicketStatus.Closed)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Closed)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Assigned)]
    [InlineData(TicketStatus.Solved, TicketStatus.Cancelled)]
    [InlineData(TicketStatus.Solved, TicketStatus.New)]
    public async Task ChangeStatusAsync_Rejects_Skipped_Steps(TicketStatus from, TicketStatus to)
    {
        var ticket = TicketWith(from);
        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().ChangeStatusAsync(ticket.Id, to, ActorId, "reason");

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.INVALID_TRANSITION", result.Error!.Code);
    }

    [Fact]
    public async Task ChangeStatusAsync_Rejects_A_No_Op_Transition()
    {
        var ticket = TicketWith(TicketStatus.InProgress);
        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().ChangeStatusAsync(
            ticket.Id, TicketStatus.InProgress, ActorId, null);

        // Without this the ticket would collect an audit row saying InProgress -> InProgress.
        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.STATUS_UNCHANGED", result.Error!.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ChangeStatusAsync_Requires_A_Reason_To_Cancel(string? reason)
    {
        var ticket = TicketWith(TicketStatus.InProgress);
        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().ChangeStatusAsync(
            ticket.Id, TicketStatus.Cancelled, ActorId, reason);

        // Cancelling is irreversible, so it has to say why -- same rule as retiring an asset.
        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.REASON_REQUIRED", result.Error!.Code);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_Cancels_When_A_Reason_Is_Given()
    {
        var ticket = TicketWith(TicketStatus.InProgress);
        var history = ArrangeMutation(ticket);

        var result = await CreateSut().ChangeStatusAsync(
            ticket.Id, TicketStatus.Cancelled, ActorId, "Duplicate of TCK-1234");

        Assert.True(result.IsSuccess);
        Assert.Equal(TicketStatus.Cancelled, ticket.Status);

        // The reason is persisted on the audit row, not discarded as it used to be.
        var entry = Assert.Single(history);
        Assert.Equal("Duplicate of TCK-1234", entry.Comment);
        Assert.Equal(TicketHistoryAction.StatusChange, entry.Action);
    }

    [Fact]
    public async Task ChangeStatusAsync_Writes_Both_Statuses_To_The_Audit_Row()
    {
        var ticket = TicketWith(TicketStatus.Assigned);
        var history = ArrangeMutation(ticket);

        await CreateSut().ChangeStatusAsync(ticket.Id, TicketStatus.InProgress, ActorId, null);

        var entry = Assert.Single(history);
        Assert.Equal(nameof(Ticket.Status), entry.FieldName);
        Assert.Equal("Assigned", entry.OldValue);
        Assert.Equal("InProgress", entry.NewValue);
        Assert.Equal(ActorId, entry.UserId);
    }

    [Fact]
    public async Task ChangeStatusAsync_Clears_SolvedAt_When_A_Ticket_Is_Reopened()
    {
        var ticket = TicketWith(TicketStatus.Solved);
        ticket.SolvedAt = DateTimeOffset.UtcNow.AddDays(-1);
        ArrangeMutation(ticket);

        await CreateSut().ChangeStatusAsync(ticket.Id, TicketStatus.InProgress, ActorId, null);

        // A reopened ticket is not solved. Leaving the stamp in place would make it look resolved
        // in every time-to-resolution report.
        Assert.Null(ticket.SolvedAt);
    }

    [Fact]
    public async Task ChangeStatusAsync_Returns_NotFound_For_A_Missing_Ticket()
    {
        var id = Guid.CreateVersion7();
        Tickets.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Ticket?)null);

        var result = await CreateSut().ChangeStatusAsync(id, TicketStatus.Assigned, ActorId, null);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.NOT_FOUND", result.Error!.Code);
    }

    // ---- Assign ------------------------------------------------------------------------------

    [Fact]
    public async Task AssignAsync_Moves_A_New_Ticket_To_Assigned()
    {
        var ticket = TicketWith(TicketStatus.New);
        var techId = Guid.CreateVersion7();
        ArrangeMutation(ticket);

        var result = await CreateSut().AssignAsync(ticket.Id, techId, null, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(TicketStatus.Assigned, ticket.Status);
        Assert.Equal(techId, ticket.AssignedUserId);
    }

    [Theory]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingForUser)]
    [InlineData(TicketStatus.WaitingForSupplier)]
    [InlineData(TicketStatus.Solved)]
    public async Task AssignAsync_Does_Not_Drag_A_Working_Ticket_Backwards(TicketStatus from)
    {
        var ticket = TicketWith(from);
        ArrangeMutation(ticket);

        var result = await CreateSut().AssignAsync(ticket.Id, Guid.CreateVersion7(), null, ActorId);

        // Reassigning must not regress the workflow. This previously forced Assigned
        // unconditionally, producing states (InProgress -> Assigned) that ChangeStatusAsync itself
        // rejects as illegal.
        Assert.True(result.IsSuccess);
        Assert.Equal(from, ticket.Status);
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task AssignAsync_Refuses_Terminal_Tickets(TicketStatus status)
    {
        var ticket = TicketWith(status);
        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().AssignAsync(ticket.Id, Guid.CreateVersion7(), null, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.ASSIGN_BLOCKED", result.Error!.Code);
    }

    [Fact]
    public async Task AssignAsync_Rejects_An_Assignment_With_No_Assignee()
    {
        var ticket = TicketWith(TicketStatus.New);
        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().AssignAsync(ticket.Id, null, null, ActorId);

        // This used to succeed and leave the ticket in status Assigned with nobody assigned --
        // "someone owns this" while nobody did.
        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.ASSIGNEE_REQUIRED", result.Error!.Code);
        Assert.Equal(TicketStatus.New, ticket.Status);
    }

    [Fact]
    public async Task AssignAsync_Accepts_A_Group_Without_A_User()
    {
        var ticket = TicketWith(TicketStatus.New);
        var groupId = Guid.CreateVersion7();
        ArrangeMutation(ticket);

        var result = await CreateSut().AssignAsync(ticket.Id, null, groupId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Null(ticket.AssignedUserId);
        Assert.Equal(groupId, ticket.AssignedGroupId);
    }

    [Fact]
    public async Task AssignAsync_Audits_The_Assignee_Change_And_The_Status_Change()
    {
        var ticket = TicketWith(TicketStatus.New);
        var previousTech = Guid.CreateVersion7();
        var newTech = Guid.CreateVersion7();
        ticket.AssignedUserId = previousTech;

        var history = ArrangeMutation(ticket);

        await CreateSut().AssignAsync(ticket.Id, newTech, null, ActorId);

        // Two rows: who it moved from/to, and the New -> Assigned transition it triggered.
        Assert.Equal(2, history.Count);

        var assignment = history.Single(h => h.Action == TicketHistoryAction.Assignment);
        Assert.Equal($"user:{previousTech}", assignment.OldValue);
        Assert.Equal($"user:{newTech}", assignment.NewValue);

        var statusChange = history.Single(h => h.Action == TicketHistoryAction.StatusChange);
        Assert.Equal("New", statusChange.OldValue);
        Assert.Equal("Assigned", statusChange.NewValue);
    }

    [Fact]
    public async Task AssignAsync_Writes_Only_An_Assignment_Row_When_Status_Is_Unchanged()
    {
        var ticket = TicketWith(TicketStatus.InProgress);
        var history = ArrangeMutation(ticket);

        await CreateSut().AssignAsync(ticket.Id, Guid.CreateVersion7(), null, ActorId);

        var entry = Assert.Single(history);
        Assert.Equal(TicketHistoryAction.Assignment, entry.Action);
    }

    // ---- Escalate ----------------------------------------------------------------------------

    [Fact]
    public async Task EscalateAsync_Steps_Level_Zero_To_One()
    {
        var ticket = TicketWith(TicketStatus.InProgress);
        ArrangeMutation(ticket);

        var result = await CreateSut().EscalateAsync(ticket.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, ticket.EscalationLevel);
        Assert.True(ticket.IsEscalated);
    }

    [Fact]
    public async Task EscalateAsync_Steps_Level_One_To_Two()
    {
        var ticket = TicketWith(TicketStatus.InProgress, escalationLevel: 1);
        ArrangeMutation(ticket);

        var result = await CreateSut().EscalateAsync(ticket.Id, ActorId);

        // Second-level escalation used to be unreachable: an IsEscalated flag check rejected the
        // step, so the model documented levels 0/1/2 while only 0/1 could ever occur.
        Assert.True(result.IsSuccess);
        Assert.Equal(2, ticket.EscalationLevel);
    }

    [Fact]
    public async Task EscalateAsync_Refuses_To_Exceed_The_Maximum_Level()
    {
        var ticket = TicketWith(TicketStatus.InProgress, escalationLevel: Ticket.MaxEscalationLevel);
        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().EscalateAsync(ticket.Id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.MAX_ESCALATION_REACHED", result.Error!.Code);
        Assert.Equal(Ticket.MaxEscalationLevel, ticket.EscalationLevel);
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task EscalateAsync_Refuses_Terminal_Tickets(TicketStatus status)
    {
        var ticket = TicketWith(status);
        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().EscalateAsync(ticket.Id, ActorId);

        // Escalation is a call for more help, meaningless once the work is finished. Every other
        // mutation guarded this; escalate did not.
        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.ESCALATE_BLOCKED", result.Error!.Code);
        Assert.Equal(0, ticket.EscalationLevel);
    }

    [Fact]
    public async Task EscalateAsync_Audits_The_Level_Change()
    {
        var ticket = TicketWith(TicketStatus.InProgress, escalationLevel: 1);
        var history = ArrangeMutation(ticket);

        await CreateSut().EscalateAsync(ticket.Id, ActorId);

        var entry = Assert.Single(history);
        Assert.Equal(nameof(Ticket.EscalationLevel), entry.FieldName);
        Assert.Equal("1", entry.OldValue);
        Assert.Equal("2", entry.NewValue);
    }

    // ---- Solve -------------------------------------------------------------------------------

    [Theory]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingForUser)]
    [InlineData(TicketStatus.WaitingForSupplier)]
    public async Task SolveAsync_Works_From_The_Statuses_The_State_Machine_Permits(TicketStatus from)
    {
        var ticket = TicketWith(from);
        ArrangeMutation(ticket);

        var result = await CreateSut().SolveAsync(ticket.Id, ActorId, "Replaced the SSD");

        Assert.True(result.IsSuccess);
        Assert.Equal(TicketStatus.Solved, ticket.Status);
        Assert.NotNull(ticket.SolvedAt);
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Assigned)]
    public async Task SolveAsync_Refuses_Tickets_Nobody_Has_Worked(TicketStatus from)
    {
        var ticket = TicketWith(from);
        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().SolveAsync(ticket.Id, ActorId, "Replaced the SSD");

        // Solve now routes through IsValidStatusTransition like every other transition. It used to
        // bypass the state machine entirely, letting a brand-new ticket jump straight to Solved --
        // a move ChangeStatusAsync rejects.
        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.SOLVE_BLOCKED", result.Error!.Code);
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task SolveAsync_Refuses_Terminal_Tickets(TicketStatus status)
    {
        var ticket = TicketWith(status);
        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().SolveAsync(ticket.Id, ActorId, "too late");

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.SOLVE_BLOCKED", result.Error!.Code);
    }

    [Fact]
    public async Task SolveAsync_Refuses_An_Already_Solved_Ticket()
    {
        var ticket = TicketWith(TicketStatus.Solved);
        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().SolveAsync(ticket.Id, ActorId, "again");

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.STATUS_UNCHANGED", result.Error!.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SolveAsync_Requires_A_Solution_Description(string? solution)
    {
        var ticket = TicketWith(TicketStatus.InProgress);
        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().SolveAsync(ticket.Id, ActorId, solution);

        // A resolution nobody described teaches nobody anything the next time the same fault lands.
        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.SOLUTION_REQUIRED", result.Error!.Code);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
    }

    [Fact]
    public async Task SolveAsync_Persists_The_Solution_On_The_Ticket()
    {
        var ticket = TicketWith(TicketStatus.InProgress);
        ArrangeMutation(ticket);

        var result = await CreateSut().SolveAsync(ticket.Id, ActorId, "  Replaced the SSD  ");

        // The solution text used to be accepted and then dropped -- there was no column for it, so
        // the technician's fix description vanished.
        Assert.True(result.IsSuccess);
        Assert.Equal("Replaced the SSD", ticket.Solution);
        Assert.Equal("Replaced the SSD", result.Value.Solution);
    }

    [Fact]
    public async Task SolveAsync_Copies_The_Solution_Into_The_Audit_Row()
    {
        var ticket = TicketWith(TicketStatus.InProgress);
        var history = ArrangeMutation(ticket);

        await CreateSut().SolveAsync(ticket.Id, ActorId, "Replaced the SSD");

        var entry = Assert.Single(history);
        Assert.Equal(TicketHistoryAction.StatusChange, entry.Action);
        Assert.Equal("Replaced the SSD", entry.Comment);
    }

    // ---- Close -------------------------------------------------------------------------------

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingForUser)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task CloseAsync_Only_Accepts_Solved_Tickets(TicketStatus from)
    {
        var ticket = TicketWith(from);
        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().CloseAsync(ticket.Id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.CLOSE_BLOCKED", result.Error!.Code);
    }

    [Fact]
    public async Task CloseAsync_Closes_A_Solved_Ticket_And_Stamps_ClosedAt()
    {
        var ticket = TicketWith(TicketStatus.Solved);
        ticket.SolvedAt = DateTimeOffset.UtcNow.AddHours(-2);
        ArrangeMutation(ticket);

        var result = await CreateSut().CloseAsync(ticket.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
        Assert.NotNull(ticket.ClosedAt);
    }

    [Fact]
    public async Task CloseAsync_Backfills_A_Missing_SolvedAt()
    {
        var ticket = TicketWith(TicketStatus.Solved);
        ticket.SolvedAt = null;
        ArrangeMutation(ticket);

        await CreateSut().CloseAsync(ticket.Id, ActorId);

        // Otherwise time-to-resolution reporting has a hole for any ticket that was reopened and
        // then closed again.
        Assert.NotNull(ticket.SolvedAt);
    }

    // ---- Update ------------------------------------------------------------------------------

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task UpdateAsync_Refuses_Terminal_Tickets(TicketStatus status)
    {
        var ticket = TicketWith(status);
        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().UpdateAsync(ticket.Id, UpdateRequest(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.UPDATE_BLOCKED", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_Recomputes_The_Score()
    {
        var ticket = TicketWith(TicketStatus.InProgress, TicketPriority.Critical);
        ArrangeMutation(ticket);

        var result = await CreateSut().UpdateAsync(
            ticket.Id, UpdateRequest(TicketPriority.Low, TicketImpact.Low, TicketUrgency.Low), ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, ticket.CalculatedScore);
    }

    [Fact]
    public async Task UpdateAsync_Does_Not_Change_Status()
    {
        var ticket = TicketWith(TicketStatus.InProgress);
        ArrangeMutation(ticket);

        await CreateSut().UpdateAsync(ticket.Id, UpdateRequest(), ActorId);

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
    }

    [Fact]
    public async Task UpdateAsync_Audits_Only_The_Fields_That_Actually_Changed()
    {
        var ticket = TicketWith(TicketStatus.InProgress, TicketPriority.Medium);
        ticket.Title = "Original title";
        ticket.Type = TicketType.Incident;

        var history = ArrangeMutation(ticket);

        // Title and priority change; type stays Incident.
        await CreateSut().UpdateAsync(
            ticket.Id,
            UpdateRequest(TicketPriority.High, TicketImpact.Medium, TicketUrgency.Medium,
                title: "New title", type: TicketType.Incident),
            ActorId);

        // Auditing every field on every save would bury the two edits that mattered.
        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.FieldName == nameof(Ticket.Title) && h.OldValue == "Original title");
        Assert.Contains(history, h => h.FieldName == nameof(Ticket.Priority) && h.NewValue == "High");
        Assert.DoesNotContain(history, h => h.FieldName == nameof(Ticket.Type));
    }

    [Fact]
    public async Task UpdateAsync_Writes_No_Audit_Rows_When_Nothing_Notable_Changed()
    {
        var ticket = TicketWith(TicketStatus.InProgress, TicketPriority.Low);
        ticket.Title = "Same title";
        ticket.Type = TicketType.Request;

        var history = ArrangeMutation(ticket);

        await CreateSut().UpdateAsync(
            ticket.Id,
            UpdateRequest(TicketPriority.Low, TicketImpact.Low, TicketUrgency.Low,
                title: "Same title", type: TicketType.Request),
            ActorId);

        Assert.Empty(history);
    }

    // ---- Delete ------------------------------------------------------------------------------

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task DeleteAsync_Allows_Untouched_And_Abandoned_Tickets(TicketStatus status)
    {
        var ticket = TicketWith(status);
        ArrangeMutation(ticket);

        var result = await CreateSut().DeleteAsync(ticket.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(ticket.IsDeleted);
        Assert.NotNull(ticket.DeletedAt);
    }

    [Theory]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Solved)]
    [InlineData(TicketStatus.Closed)]
    public async Task DeleteAsync_Refuses_Tickets_That_Have_Been_Worked(TicketStatus status)
    {
        var ticket = TicketWith(status);
        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().DeleteAsync(ticket.Id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.DELETE_BLOCKED", result.Error!.Code);
        Assert.False(ticket.IsDeleted);
    }

    [Fact]
    public async Task DeleteAsync_Audits_The_Deletion()
    {
        var ticket = TicketWith(TicketStatus.New);
        var history = ArrangeMutation(ticket);

        await CreateSut().DeleteAsync(ticket.Id, ActorId);

        var entry = Assert.Single(history);
        Assert.Equal(TicketHistoryAction.Delete, entry.Action);
        Assert.Equal(ActorId, entry.UserId);
    }
}
