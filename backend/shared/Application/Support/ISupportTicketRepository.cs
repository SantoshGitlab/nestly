using Nestly.Domain;

namespace Nestly.Application.Support;

public interface ISupportTicketRepository
{
    Task AddAsync(SupportTicket ticket);

    Task UpdateAsync(SupportTicket ticket);

    /// <summary>Loaded with its comment thread - a ticket is never useful partially loaded.</summary>
    Task<SupportTicket?> GetByIdAsync(Guid id);

    Task<IReadOnlyList<SupportTicket>> ListByCustomerAsync(Guid customerId);

    Task<IReadOnlyList<SupportTicket>> ListByBookingAsync(Guid bookingId);
}
