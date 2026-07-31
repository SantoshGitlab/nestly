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

    /// <summary>One ticket joined with its customer/assignee display names, for the admin detail screen (task 120f). Null if no ticket with that id exists.</summary>
    Task<AdminSupportTicketRow?> GetAdminRowByIdAsync(Guid id);

    /// <summary>Filtered/paginated admin ticket search across every customer (SRS 12.14.1, task 120f) - unlike <see cref="ListByCustomerAsync"/>, not scoped to a single customer.</summary>
    Task<AdminSupportTicketSearchResult> SearchAsync(AdminSupportTicketCriteria criteria, int page, int pageSize);
}
