namespace Daraban.Platform.Contracts.ServiceDesk;

public sealed record TicketCreatedEvent(Guid TicketId, Guid EntityId, Guid? RequesterId);

public sealed record TicketRaisedEvent(Guid TicketId, Guid EntityId);
