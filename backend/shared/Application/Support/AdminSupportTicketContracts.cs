using Nestly.Domain;

namespace Nestly.Application.Support;

/// <summary>
/// The admin ticket workflow (SRS 12.14, 16.2, tasks 120a-f): list/search
/// across every customer's tickets, assign/respond/escalate/resolve, and
/// surface the linked booking - everything <see cref="ISupportTicketService"/>
/// deliberately does not cover since that interface is customer-identity-
/// scoped by design (every one of its methods filters by the caller's own
/// <c>customerId</c>). This file's contracts mirror
/// <see cref="SupportTicketContracts"/>'s shapes plus the admin-only fields
/// (assignee, escalation timestamp, linked booking summary) SRS 16.3 adds.
/// </summary>

/// <summary>Filter criteria for the admin ticket list (SRS 12.14.1: Ticket ID, Booking ID, Customer, Category, Priority, Status, Assigned agent, Date range). All fields are optional and combine with AND.</summary>
public sealed record AdminSupportTicketCriteria(
    Guid? CustomerId,
    Guid? BookingId,
    SupportTicketCategory? Category,
    SupportTicketPriority? Priority,
    SupportTicketStatus? Status,
    Guid? AssignedAdminUserId,
    /// <summary>When true, only unassigned tickets; when false, only assigned ones; null does not filter on assignment.</summary>
    bool? Unassigned,
    DateTime? FromUtc,
    DateTime? ToUtc);

/// <summary>Query-string shape of an admin ticket search request (task 120f). Paging follows the same Page/PageSize convention as <c>ReviewModerationSearchRequest</c>.</summary>
public sealed record AdminSupportTicketSearchRequest(
    Guid? CustomerId,
    Guid? BookingId,
    SupportTicketCategory? Category,
    SupportTicketPriority? Priority,
    SupportTicketStatus? Status,
    Guid? AssignedAdminUserId,
    bool? Unassigned,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 20)
{
    public AdminSupportTicketCriteria ToCriteria() =>
        new(CustomerId, BookingId, Category, Priority, Status, AssignedAdminUserId, Unassigned, FromUtc, ToUtc);
}

/// <summary>One ticket joined with the customer/assignee display names the admin list shows - the ticket itself is never denormalized, only read alongside its related names (same convention as <c>ReviewModerationRow</c>).</summary>
public sealed record AdminSupportTicketRow(SupportTicket Ticket, string CustomerName, string? AssignedAdminName);

/// <summary>A page of <see cref="AdminSupportTicketRow"/> plus the total match count, for pagination.</summary>
public sealed record AdminSupportTicketSearchResult(IReadOnlyList<AdminSupportTicketRow> Rows, int TotalCount);

/// <summary>One ticket row as shown on the admin ticket list screen (SRS 12.14.1, task 120f).</summary>
public sealed record AdminSupportTicketSummaryResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid? BookingId,
    SupportTicketCategory Category,
    SupportTicketPriority Priority,
    string Subject,
    SupportTicketStatus Status,
    bool IsDisputed,
    Guid? AssignedAdminUserId,
    string? AssignedAdminName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>A page of admin ticket rows, newest first, with the total match count for pagination.</summary>
public sealed record AdminSupportTicketSearchResponse(
    IReadOnlyList<AdminSupportTicketSummaryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// The linked booking's summary (task 120e "link booking actions") - just
/// enough for an admin working a ticket to see what booking it concerns
/// without leaving the ticket screen. No cancel/refund action lives here yet:
/// there is no admin booking-management API in this codebase to call into
/// (BookingMgmt is a separate, not-yet-landed vertical) - see
/// <c>SupportTicketsController</c>'s doc comment for the wiring TODO once it
/// exists.
/// </summary>
public sealed record LinkedBookingSummaryResponse(
    Guid Id,
    BookingStatus Status,
    string CustomerNameSnapshot,
    DateOnly SlotDate,
    TimeSpan SlotStartTimeSnapshot,
    TimeSpan SlotEndTimeSnapshot,
    decimal TotalPayableSnapshot);

/// <summary>Full admin ticket detail: everything <see cref="SupportTicketDetailResponse"/> has, plus the assignee and linked-booking summary an admin needs (SRS 16.3, tasks 120a-e).</summary>
public sealed record AdminSupportTicketDetailResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid? BookingId,
    LinkedBookingSummaryResponse? Booking,
    SupportTicketCategory Category,
    SupportTicketPriority Priority,
    string Subject,
    string Description,
    SupportTicketStatus Status,
    string? ResolutionSummary,
    bool IsDisputed,
    DisputeResolutionOutcome? DisputeOutcome,
    Guid? AssignedAdminUserId,
    string? AssignedAdminName,
    DateTime? AssignedAtUtc,
    DateTime? EscalatedAtUtc,
    IReadOnlyList<SupportTicketCommentResponse> Comments,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>Assigns a ticket to an admin/agent (SRS 12.14.2 "Assign to team/user", task 120a).</summary>
public sealed record AssignSupportTicketRequest(Guid AdminUserId);

/// <summary>Moves a ticket to Resolved (task 120d) - the generic counterpart to <see cref="ResolveDisputeRequest"/>, used whenever the ticket has no formal dispute attached.</summary>
public sealed record ResolveSupportTicketRequest(string ResolutionSummary);

/// <summary>Attaches a booking to a ticket after creation (SRS 12.14.2 "Link ... booking action", task 120e).</summary>
public sealed record LinkSupportTicketBookingRequest(Guid BookingId);

/// <summary>An admin/agent selectable in the "assign to" dropdown (task 120a) - active admin users only.</summary>
public sealed record AssignableAdminResponse(Guid Id, string FullName, string Email);
