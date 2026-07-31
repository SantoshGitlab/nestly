using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain.Events;

/// <summary>Raised whenever a support ticket's status changes (task 88g - ticket-update notifications).</summary>
public sealed record SupportTicketStatusChangedEvent(Guid TicketId, Guid CustomerId, SupportTicketStatus FromStatus, SupportTicketStatus ToStatus) : DomainEvent;
