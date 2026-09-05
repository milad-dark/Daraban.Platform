using Daraban.Modules.ServiceDesk.Data.Entities;
using Daraban.Modules.ServiceDesk.Data.Repositories;
using Daraban.Modules.ServiceDesk.Services;
using Daraban.Modules.ServiceDesk.Services.Dtos;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Contracts.ServiceDesk;
using Moq;
using Xunit;

namespace Daraban.Modules.ServiceDesk.Tests;

/// <summary>
/// Shared fixture plumbing for the TicketService suites below. The ITIL status machine is the
/// centre of gravity: it is the only thing stopping a closed ticket from being reopened,
/// re-solved or edited, so it gets exhaustive coverage of both allowed and forbidden paths.
/// </summary>
public abstract class TicketServiceTestBase
{
    protected static readonly Guid EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid RequesterId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    protected readonly Mock<ITicketRepository> Tickets = new(MockBehavior.Strict);
    protected readonly Mock<IEventPublisher> Events = new();

    protected TicketService CreateSut() => new(Tickets.Object, Events.Object);

    protected static Ticket TicketWith(
        TicketStatus status = TicketStatus.New,
        TicketPriority priority = TicketPriority.Medium,
        int escalationLevel = 0)
        => new()
        {
            Id = Guid.CreateVersion7(),
            EntityId = EntityId,
            Type = TicketType.Incident,
            Status = status,
            Priority = priority,
            Impact = TicketImpact.Medium,
            Urgency = TicketUrgency.Medium,
            Title = "Laptop will not boot",
            RequesterUserId = RequesterId,
            EscalationLevel = escalationLevel,
            IsEscalated = escalationLevel > 0,
            OpenedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    protected static CreateTicketRequest CreateRequest(
        TicketPriority priority = TicketPriority.High,
        TicketImpact impact = TicketImpact.High,
        TicketUrgency urgency = TicketUrgency.Medium,
        Guid? requesterId = null,
        Guid? assignedUserId = null,
        Guid? assignedGroupId = null)
        => new(TicketType.Incident, priority, impact, urgency,
            "Laptop will not boot", "Blue screen on startup",
            requesterId ?? RequesterId, assignedUserId, assignedGroupId,
            null, null, null, null, TicketSource.Helpdesk);

    protected static UpdateTicketRequest UpdateRequest(
        TicketPriority priority = TicketPriority.Low,
        TicketImpact impact = TicketImpact.Low,
        TicketUrgency urgency = TicketUrgency.Low,
        string title = "Renamed",
        TicketType type = TicketType.Request)
        => new(type, priority, impact, urgency, title, "New description",
            null, null, null, null, null, null);

    /// <summary>
    /// Happy-path arrange for a single-ticket mutation. Returns the list that every audit row
    /// written during the call is appended to, so history assertions never depend on setup order --
    /// a second Setup for AddHistoryAsync would silently replace the capturing callback.
    /// </summary>
    protected List<TicketHistory> ArrangeMutation(Ticket ticket)
    {
        var history = new List<TicketHistory>();

        Tickets.Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        Tickets.Setup(r => r.UpdateAsync(ticket, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Tickets.Setup(r => r.AddHistoryAsync(It.IsAny<TicketHistory>(), It.IsAny<CancellationToken>()))
            .Callback<TicketHistory, CancellationToken>((h, _) => history.Add(h))
            .Returns(Task.CompletedTask);
        Tickets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return history;
    }

    /// <summary>Captures audit rows for calls that do not go through ArrangeMutation (Create).</summary>
    protected List<TicketHistory> CaptureHistory()
    {
        var captured = new List<TicketHistory>();
        Tickets.Setup(r => r.AddHistoryAsync(It.IsAny<TicketHistory>(), It.IsAny<CancellationToken>()))
            .Callback<TicketHistory, CancellationToken>((h, _) => captured.Add(h))
            .Returns(Task.CompletedTask);
        return captured;
    }
}

// ---- Create ----------------------------------------------------------------------------------

public class TicketServiceCreateTests : TicketServiceTestBase
{
    private void ArrangeCreate(Action<Ticket>? onAdd = null)
    {
        Tickets.Setup(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>((t, _) => onAdd?.Invoke(t))
            .Returns(Task.CompletedTask);
        Tickets.Setup(r => r.AddHistoryAsync(It.IsAny<TicketHistory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Tickets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task CreateAsync_Scopes_The_Ticket_To_The_Callers_Entity_Not_The_Actor()
    {
        Ticket? captured = null;
        ArrangeCreate(t => captured = t);

        var result = await CreateSut().CreateAsync(CreateRequest(), EntityId, ActorId);

        Assert.True(result.IsSuccess);

        // Regression guard: this used to be `EntityId = actorUserId`, which wrote a USER id into
        // the tenant column. Every created ticket landed in a nonexistent entity and was invisible
        // to GetPagedAsync, which filters on the caller's ActiveEntityId.
        Assert.Equal(EntityId, captured!.EntityId);
        Assert.NotEqual(ActorId, captured.EntityId);
        Assert.Equal(EntityId, result.Value.EntityId);
    }

    [Fact]
    public async Task CreateAsync_Rejects_An_Empty_Entity()
    {
        var result = await CreateSut().CreateAsync(CreateRequest(), Guid.Empty, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.ENTITY_REQUIRED", result.Error!.Code);
        Tickets.Verify(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Rejects_A_Missing_Requester()
    {
        var result = await CreateSut().CreateAsync(
            CreateRequest(requesterId: Guid.Empty), EntityId, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET.REQUESTER_REQUIRED", result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_Opens_An_Unassigned_Ticket_As_New()
    {
        Ticket? captured = null;
        ArrangeCreate(t => captured = t);

        await CreateSut().CreateAsync(CreateRequest(), EntityId, ActorId);

        Assert.Equal(TicketStatus.New, captured!.Status);
        Assert.Equal(ActorId, captured.CreatedById);
        Assert.Equal(RequesterId, captured.RequesterUserId);
    }

    [Fact]
    public async Task CreateAsync_Opens_A_PreAssigned_Ticket_As_Assigned()
    {
        Ticket? captured = null;
        ArrangeCreate(t => captured = t);
        var techId = Guid.CreateVersion7();

        await CreateSut().CreateAsync(
            CreateRequest(assignedUserId: techId), EntityId, ActorId);

        // Status must not contradict the fact that somebody already owns the ticket.
        Assert.Equal(TicketStatus.Assigned, captured!.Status);
        Assert.Equal(techId, captured.AssignedUserId);
    }

    [Fact]
    public async Task CreateAsync_Opens_A_GroupAssigned_Ticket_As_Assigned()
    {
        Ticket? captured = null;
        ArrangeCreate(t => captured = t);

        await CreateSut().CreateAsync(
            CreateRequest(assignedGroupId: Guid.CreateVersion7()), EntityId, ActorId);

        Assert.Equal(TicketStatus.Assigned, captured!.Status);
    }

    [Theory]
    [InlineData(TicketPriority.Low, TicketImpact.Low, TicketUrgency.Low, 1)]
    [InlineData(TicketPriority.Medium, TicketImpact.Medium, TicketUrgency.Medium, 8)]
    [InlineData(TicketPriority.High, TicketImpact.High, TicketUrgency.High, 27)]
    [InlineData(TicketPriority.Critical, TicketImpact.High, TicketUrgency.High, 45)]
    [InlineData(TicketPriority.VeryHigh, TicketImpact.Low, TicketUrgency.Medium, 8)]
    public async Task CreateAsync_Computes_The_Priority_Score(
        TicketPriority priority, TicketImpact impact, TicketUrgency urgency, int expected)
    {
        Ticket? captured = null;
        ArrangeCreate(t => captured = t);

        await CreateSut().CreateAsync(CreateRequest(priority, impact, urgency), EntityId, ActorId);

        Assert.Equal(expected, captured!.CalculatedScore);
    }

    [Fact]
    public async Task CreateAsync_Uses_A_Sortable_Uuidv7_Id()
    {
        Ticket? captured = null;
        ArrangeCreate(t => captured = t);

        await CreateSut().CreateAsync(CreateRequest(), EntityId, ActorId);

        // Version 7, not 4 -- random ids scatter inserts across a table clustered by primary key.
        Assert.Equal(7, captured!.Id.Version);
    }

    [Fact]
    public async Task CreateAsync_Writes_A_Create_Audit_Row()
    {
        var history = CaptureHistory();
        Tickets.Setup(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Tickets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().CreateAsync(CreateRequest(), EntityId, ActorId);

        var entry = Assert.Single(history);
        Assert.Equal(TicketHistoryAction.Create, entry.Action);
        Assert.Equal(ActorId, entry.UserId);
        Assert.Equal(nameof(Ticket.Status), entry.FieldName);
        Assert.Equal(TicketStatus.New.ToString(), entry.NewValue);
    }

    [Fact]
    public async Task CreateAsync_Publishes_TicketCreated()
    {
        Ticket? captured = null;
        ArrangeCreate(t => captured = t);

        await CreateSut().CreateAsync(CreateRequest(), EntityId, ActorId);

        Events.Verify(e => e.PublishAsync(
            It.Is<TicketCreatedEvent>(evt =>
                evt.TicketId == captured!.Id &&
                evt.EntityId == EntityId &&
                evt.RequesterId == RequesterId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
