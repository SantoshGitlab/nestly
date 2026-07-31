using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Support;

/// <summary>Customer-facing support ticket workflow (SRS 11.18, 16, 24.8, tasks 86a-d).</summary>
public interface ISupportTicketService
{
    /// <summary>Creates a ticket - booking-linked or generic depending on category (SRS 11.18.1-2, task 86d).</summary>
    Task<Result<SupportTicketDetailResponse>> CreateAsync(Guid customerId, CreateSupportTicketRequest request);

    /// <summary>All of the caller's tickets, newest first (task 86b).</summary>
    Task<Result<IReadOnlyList<SupportTicketSummaryResponse>>> ListAsync(Guid customerId);

    /// <summary>The caller's tickets linked to a specific booking (SRS 11.18.2, task 86d).</summary>
    Task<Result<IReadOnlyList<SupportTicketSummaryResponse>>> ListByBookingAsync(Guid customerId, Guid bookingId);

    /// <summary>Full detail including the comment thread (SRS 11.18.3, task 86c).</summary>
    Task<Result<SupportTicketDetailResponse>> GetDetailAsync(Guid customerId, Guid ticketId);

    /// <summary>Appends a customer follow-up to the thread (SRS 11.18.3, 12.14.2).</summary>
    Task<Result<SupportTicketDetailResponse>> AddCommentAsync(Guid customerId, Guid ticketId, AddSupportTicketCommentRequest request);
}
