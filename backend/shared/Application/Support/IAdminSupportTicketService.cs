using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Support;

/// <summary>
/// Admin ticket workflow (SRS 12.14, 16.2, tasks 120a-e): search/list across
/// every customer, view detail, assign, respond, escalate, resolve/close, and
/// link a booking. Separate from <see cref="ISupportTicketService"/> (which
/// is customer-identity-scoped) and <see cref="IDisputeResolutionService"/>
/// (which only covers the formal dispute mark/resolve sub-flow, task 155) -
/// this is the general-purpose admin workflow the rest of a ticket's life
/// goes through.
/// </summary>
public interface IAdminSupportTicketService
{
    /// <summary>Filtered/paginated ticket search across every customer (SRS 12.14.1, task 120f).</summary>
    Task<Result<AdminSupportTicketSearchResponse>> SearchAsync(AdminSupportTicketSearchRequest request);

    /// <summary>Full admin detail - comment thread, assignee, and linked booking summary if any (SRS 16.3, task 120f).</summary>
    Task<Result<AdminSupportTicketDetailResponse>> GetDetailAsync(Guid ticketId);

    /// <summary>Every active admin, for the "assign to" picker (task 120a).</summary>
    Task<Result<IReadOnlyList<AssignableAdminResponse>>> ListAssignableAdminsAsync();

    /// <summary>Assigns a ticket to an admin/agent (SRS 12.14.2 "Assign to team/user", task 120a).</summary>
    Task<Result<AdminSupportTicketDetailResponse>> AssignAsync(Guid ticketId, AssignSupportTicketRequest request);

    /// <summary>Clears a ticket's current assignment, returning it to the unassigned pool (task 120a).</summary>
    Task<Result<AdminSupportTicketDetailResponse>> UnassignAsync(Guid ticketId);

    /// <summary>Appends an admin response to the ticket's comment thread (SRS 12.14.2 "Add response/note", task 120b).</summary>
    Task<Result<AdminSupportTicketDetailResponse>> RespondAsync(Guid ticketId, AddSupportTicketCommentRequest request);

    /// <summary>Moves the ticket to Escalated (SRS 12.14.2 "Mark escalated", task 120c).</summary>
    Task<Result<AdminSupportTicketDetailResponse>> EscalateAsync(Guid ticketId);

    /// <summary>Moves the ticket to Resolved (SRS 12.14.2 "Mark resolved/closed", task 120d) - for tickets with no formal dispute; use <see cref="IDisputeResolutionService"/> instead when one is open.</summary>
    Task<Result<AdminSupportTicketDetailResponse>> ResolveAsync(Guid ticketId, ResolveSupportTicketRequest request);

    /// <summary>Moves the ticket to Closed (SRS 12.14.2 "Mark resolved/closed", task 120d) - only valid from Resolved or, per <see cref="Nestly.Domain.SupportTicketLifecycle"/>'s duplicate/spam allowance, directly from Open.</summary>
    Task<Result<AdminSupportTicketDetailResponse>> CloseAsync(Guid ticketId);

    /// <summary>Attaches (or re-attaches) a booking to the ticket (SRS 12.14.2 "Link ... booking action", task 120e).</summary>
    Task<Result<AdminSupportTicketDetailResponse>> LinkBookingAsync(Guid ticketId, LinkSupportTicketBookingRequest request);
}
