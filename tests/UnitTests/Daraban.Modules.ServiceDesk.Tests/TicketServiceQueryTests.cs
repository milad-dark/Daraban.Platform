using Daraban.Modules.ServiceDesk.Data.Entities;
using Daraban.Modules.ServiceDesk.Data.Repositories;
using Daraban.Modules.ServiceDesk.Services;
using Daraban.Modules.ServiceDesk.Services.Dtos;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.ServiceDesk.Tests;

/// <summary>
/// TicketService read paths: paging normalisation, the open/overdue dashboard counts, and the
/// audit-trail read. The counts matter because a wrong number on a dashboard is worse than no
/// number — it looks authoritative.
/// </summary>
public class TicketServiceQueryTests : TicketServiceTestBase
{
    [Fact]
    public async Task GetByIdAsync_Returns_NotFound_For_A_Missing_Ticket()
    {
        var id = Guid.CreateVersion7();
        Tickets.Setup(r => r.GetWithDetailsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Ticket?)null);

        var result = await CreateSut().GetByIdAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.NOT_FOUND", result.Error!.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task GetByIdAsync_Exposes_The_Tenant_And_Solution()
    {
        var ticket = TicketWith(TicketStatus.Solved);
        ticket.Solution = "Replaced the SSD";
        Tickets.Setup(r => r.GetWithDetailsAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

        var result = await CreateSut().GetByIdAsync(ticket.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(EntityId, result.Value.EntityId);
        Assert.Equal("Replaced the SSD", result.Value.Solution);
    }

    [Fact]
    public async Task GetPagedAsync_Passes_Every_Filter_Through()
    {
        var assignedUserId = Guid.CreateVersion7();

        Tickets.Setup(r => r.GetPagedAsync(
                EntityId, TicketType.Incident, TicketStatus.InProgress, TicketPriority.High,
                assignedUserId, null, "boot", 2, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Ticket>(), 0));

        var result = await CreateSut().GetPagedAsync(
            EntityId, TicketType.Incident, TicketStatus.InProgress, TicketPriority.High,
            assignedUserId, null, "boot", 2, 50);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(50, result.Value.PageSize);
    }

    [Theory]
    [InlineData(0, 20, 1, 20)]      // page below 1 is clamped up
    [InlineData(-5, 20, 1, 20)]
    [InlineData(3, 0, 3, 20)]       // pageSize below 1 falls back to the default
    [InlineData(1, 5000, 1, 200)]   // capped so one request cannot pull the whole table
    public async Task GetPagedAsync_Normalizes_Paging(
        int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        Tickets.Setup(r => r.GetPagedAsync(
                EntityId, null, null, null, null, null, null, expectedPage, expectedPageSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Ticket>(), 0));

        var result = await CreateSut().GetPagedAsync(
            EntityId, null, null, null, null, null, null, page, pageSize);

        // An unclamped page of 0 produces Skip(-pageSize), which Postgres rejects outright.
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedPage, result.Value.Page);
        Assert.Equal(expectedPageSize, result.Value.PageSize);
    }

    [Fact]
    public async Task GetPagedAsync_Echoes_The_Repository_Total()
    {
        var page = new[] { TicketWith(), TicketWith() };

        Tickets.Setup(r => r.GetPagedAsync(
                EntityId, null, null, null, null, null, null, 1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((page, 37));

        var result = await CreateSut().GetPagedAsync(EntityId, null, null, null, null, null, null, 1, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(37, result.Value.TotalCount);
        Assert.Equal(2, result.Value.Items.Count);
    }

    // ---- Dashboard counts --------------------------------------------------------------------

    [Fact]
    public async Task GetOpenCountAsync_Delegates_To_A_Dedicated_Count_Query()
    {
        Tickets.Setup(r => r.CountOpenAsync(EntityId, It.IsAny<CancellationToken>())).ReturnsAsync(42);

        var result = await CreateSut().GetOpenCountAsync(EntityId);

        // Previously this asked GetPagedAsync for a single page filtered to status New and read its
        // TotalCount -- which both under-counted (Assigned/InProgress/Waiting* are all open) and
        // made the database sort and page rows nobody looked at.
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Tickets.Verify(r => r.CountOpenAsync(EntityId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOverdueCountAsync_Queries_Against_The_Current_Time()
    {
        var before = DateTimeOffset.UtcNow;
        DateTimeOffset captured = default;

        Tickets.Setup(r => r.CountOverdueAsync(EntityId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTimeOffset, CancellationToken>((_, asOf, _) => captured = asOf)
            .ReturnsAsync(3);

        var result = await CreateSut().GetOverdueCountAsync(EntityId);

        // This used to be `return Result<int>.Success(0)` behind a TODO, so the dashboard always
        // claimed zero overdue tickets -- worse than showing nothing at all.
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value);
        Assert.InRange(captured, before, DateTimeOffset.UtcNow);
    }

    // ---- Audit trail read ---------------------------------------------------------------------

    [Fact]
    public async Task GetHistoryAsync_Returns_NotFound_For_A_Missing_Ticket()
    {
        var id = Guid.CreateVersion7();
        Tickets.Setup(r => r.ExistsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateSut().GetHistoryAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task GetHistoryAsync_Maps_Every_Audit_Row()
    {
        var ticketId = Guid.CreateVersion7();
        var rows = new[]
        {
            TicketHistory.Record(ticketId, ActorId, nameof(Ticket.Status), "New", "Assigned",
                TicketHistoryAction.StatusChange),
            TicketHistory.Record(ticketId, ActorId, nameof(Ticket.Status), "Assigned", "InProgress",
                TicketHistoryAction.StatusChange, "Started work"),
        };

        Tickets.Setup(r => r.ExistsAsync(ticketId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Tickets.Setup(r => r.GetHistoryAsync(ticketId, It.IsAny<CancellationToken>())).ReturnsAsync(rows);

        var result = await CreateSut().GetHistoryAsync(ticketId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("Started work", result.Value[1].Comment);
        Assert.All(result.Value, dto => Assert.Equal(ActorId, dto.UserId));
    }

    [Fact]
    public async Task GetHistoryAsync_Returns_An_Empty_List_For_A_Ticket_With_No_History()
    {
        var ticketId = Guid.CreateVersion7();
        Tickets.Setup(r => r.ExistsAsync(ticketId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Tickets.Setup(r => r.GetHistoryAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TicketHistory>());

        var result = await CreateSut().GetHistoryAsync(ticketId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    // ---- Shared helpers ----------------------------------------------------------------------

    [Theory]
    [InlineData(TicketStatus.New, true)]
    [InlineData(TicketStatus.Assigned, true)]
    [InlineData(TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.WaitingForUser, true)]
    [InlineData(TicketStatus.WaitingForSupplier, true)]
    [InlineData(TicketStatus.Solved, false)]
    [InlineData(TicketStatus.Closed, false)]
    [InlineData(TicketStatus.Cancelled, false)]
    public void TicketStatuses_IsOpen_Covers_Everything_Awaiting_Work(TicketStatus status, bool expected)
    {
        // Solved is excluded: the work is done, only closure validation remains.
        Assert.Equal(expected, TicketStatuses.IsOpen(status));
    }

    [Theory]
    [InlineData(TicketStatus.Closed, true)]
    [InlineData(TicketStatus.Cancelled, true)]
    [InlineData(TicketStatus.Solved, false)]
    [InlineData(TicketStatus.New, false)]
    public void TicketStatuses_IsTerminal_Only_Covers_Closed_And_Cancelled(
        TicketStatus status, bool expected)
    {
        Assert.Equal(expected, TicketStatuses.IsTerminal(status));
    }

    [Fact]
    public void TicketStatuses_Open_And_Terminal_Do_Not_Overlap()
    {
        Assert.Empty(TicketStatuses.Open.Intersect(TicketStatuses.Terminal));
    }
}
