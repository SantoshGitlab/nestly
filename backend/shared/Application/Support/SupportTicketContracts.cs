using Nestly.Domain;

namespace Nestly.Application.Support;

/// <summary>Ticket creation request (SRS 11.18.2, 16.1, task 86a). BookingId is mandatory for booking-related categories, optional for generic ones (task 86d) - see <c>SupportTicketService</c>.</summary>
public record CreateSupportTicketRequest(
    SupportTicketCategory Category, Guid? BookingId, string Subject, string Description, SupportTicketPriority? Priority);

public record AddSupportTicketCommentRequest(string Comment);

/// <summary>Ticket list row (SRS 11.18.3, task 86b).</summary>
public record SupportTicketSummaryResponse(
    Guid Id,
    SupportTicketCategory Category,
    SupportTicketPriority Priority,
    string Subject,
    SupportTicketStatus Status,
    Guid? BookingId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record SupportTicketCommentResponse(Guid Id, SupportTicketCommentAuthorType AuthorType, string Comment, DateTime CreatedAt);

/// <summary>Full ticket detail with its comment thread (SRS 11.18.3, task 86c).</summary>
public record SupportTicketDetailResponse(
    Guid Id,
    Guid CustomerId,
    Guid? BookingId,
    SupportTicketCategory Category,
    SupportTicketPriority Priority,
    string Subject,
    string Description,
    SupportTicketStatus Status,
    string? ResolutionSummary,
    bool IsDisputed,
    DisputeResolutionOutcome? DisputeOutcome,
    IReadOnlyList<SupportTicketCommentResponse> Comments,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
