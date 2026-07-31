using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Bookings;
using Nestly.Application.Support;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Admin ticket workflow (SRS 12.14, 16.2, tasks 120a-e): search/list, detail,
/// assign/unassign, respond, escalate, resolve/close, and link a booking.
/// Reuses <see cref="SupportTicket"/>'s own behavior methods for every
/// mutation (<c>AssignTo</c>, <c>Escalate</c>, <c>ChangeStatus</c>,
/// <c>LinkBooking</c>) rather than mutating its properties directly - the
/// aggregate enforces its own invariants (valid status transitions, the
/// escalation timestamp), this service only adds the admin-facing lookups
/// (assignee existence, booking ownership) around them.
/// </summary>
/// <remarks>
/// Writes an audit entry for every mutation (task 132c gap fix): assigning,
/// escalating and resolving a ticket are exactly the SRS 12.14.2 admin
/// actions the audit trail is meant to cover, the same reasoning
/// <c>AdminUserManagementService</c>'s doc comment gives for its own writes.
/// The entry is staged before the repository call so the repository's own
/// <c>SaveChangesAsync</c> commits both in one transaction.
/// </remarks>
public class AdminSupportTicketService : IAdminSupportTicketService
{
    private readonly ISupportTicketRepository _ticketRepository;
    private readonly IAdminUserRepository _adminUserRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IAuditLogWriter _auditLogWriter;

    public AdminSupportTicketService(
        ISupportTicketRepository ticketRepository,
        IAdminUserRepository adminUserRepository,
        IBookingRepository bookingRepository,
        IAuditLogWriter auditLogWriter)
    {
        _ticketRepository = ticketRepository;
        _adminUserRepository = adminUserRepository;
        _bookingRepository = bookingRepository;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<Result<AdminSupportTicketSearchResponse>> SearchAsync(AdminSupportTicketSearchRequest request)
    {
        var result = await _ticketRepository.SearchAsync(request.ToCriteria(), request.Page, request.PageSize);
        var items = result.Rows.Select(ToSummary).ToList();
        return Result.Success(new AdminSupportTicketSearchResponse(items, result.TotalCount, request.Page, request.PageSize));
    }

    public async Task<Result<AdminSupportTicketDetailResponse>> GetDetailAsync(Guid ticketId)
    {
        var row = await _ticketRepository.GetAdminRowByIdAsync(ticketId);
        if (row is null)
        {
            return Error.NotFound("SupportTicket.NotFound", "The specified ticket does not exist.");
        }

        return Result.Success(await ToDetailResponseAsync(row));
    }

    public async Task<Result<IReadOnlyList<AssignableAdminResponse>>> ListAssignableAdminsAsync()
    {
        var admins = await _adminUserRepository.ListActiveAsync();
        IReadOnlyList<AssignableAdminResponse> response =
            admins.Select(a => new AssignableAdminResponse(a.Id, a.FullName, a.Email)).ToList();
        return Result.Success(response);
    }

    public async Task<Result<AdminSupportTicketDetailResponse>> AssignAsync(Guid ticketId, AssignSupportTicketRequest request)
    {
        var row = await _ticketRepository.GetAdminRowByIdAsync(ticketId);
        if (row is null)
        {
            return Error.NotFound("SupportTicket.NotFound", "The specified ticket does not exist.");
        }

        var admin = await _adminUserRepository.GetByIdAsync(request.AdminUserId);
        if (admin is null || admin.Status != AdminUserStatus.Active)
        {
            return Error.Validation("SupportTicket.AssigneeNotFound", "The specified admin user does not exist or is not active.");
        }

        try
        {
            row.Ticket.AssignTo(request.AdminUserId);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Business("SupportTicket.CannotAssign", ex.Message);
        }

        await _auditLogWriter.WriteAsync(new AuditEntry("SupportTicket", ticketId.ToString(), "Assigned", NewValues: request.AdminUserId.ToString()));
        await _ticketRepository.UpdateAsync(row.Ticket);
        return Result.Success(await ToDetailResponseAsync(new AdminSupportTicketRow(row.Ticket, row.CustomerName, admin.FullName)));
    }

    public async Task<Result<AdminSupportTicketDetailResponse>> UnassignAsync(Guid ticketId)
    {
        var row = await _ticketRepository.GetAdminRowByIdAsync(ticketId);
        if (row is null)
        {
            return Error.NotFound("SupportTicket.NotFound", "The specified ticket does not exist.");
        }

        row.Ticket.Unassign();
        await _auditLogWriter.WriteAsync(new AuditEntry("SupportTicket", ticketId.ToString(), "Unassigned"));
        await _ticketRepository.UpdateAsync(row.Ticket);
        return Result.Success(await ToDetailResponseAsync(new AdminSupportTicketRow(row.Ticket, row.CustomerName, null)));
    }

    public async Task<Result<AdminSupportTicketDetailResponse>> RespondAsync(Guid ticketId, AddSupportTicketCommentRequest request)
    {
        var row = await _ticketRepository.GetAdminRowByIdAsync(ticketId);
        if (row is null)
        {
            return Error.NotFound("SupportTicket.NotFound", "The specified ticket does not exist.");
        }

        // An admin's first response is what moves a freshly-opened ticket
        // into active investigation (SRS 12.14.2's "Change status" action) -
        // the same auto-transition SupportTicket.MarkDisputed already applies
        // for the dispute sub-flow. Without this, a ticket could never
        // legally reach Resolved (SupportTicketLifecycle has no direct
        // Open -> Resolved edge) unless it happened to be escalated first. A
        // ticket already past Open is left exactly as it is - AddComment
        // itself never changes status.
        if (row.Ticket.Status == SupportTicketStatus.Open)
        {
            row.Ticket.ChangeStatus(SupportTicketStatus.InProgress);
        }

        row.Ticket.AddComment(Guid.NewGuid(), SupportTicketCommentAuthorType.Support, request.Comment);
        await _auditLogWriter.WriteAsync(new AuditEntry("SupportTicket", ticketId.ToString(), "Responded"));
        await _ticketRepository.UpdateAsync(row.Ticket);
        return Result.Success(await ToDetailResponseAsync(row));
    }

    public async Task<Result<AdminSupportTicketDetailResponse>> EscalateAsync(Guid ticketId)
    {
        var row = await _ticketRepository.GetAdminRowByIdAsync(ticketId);
        if (row is null)
        {
            return Error.NotFound("SupportTicket.NotFound", "The specified ticket does not exist.");
        }

        try
        {
            row.Ticket.Escalate();
        }
        catch (InvalidOperationException ex)
        {
            return Error.Business("SupportTicket.CannotEscalate", ex.Message);
        }

        await _auditLogWriter.WriteAsync(new AuditEntry("SupportTicket", ticketId.ToString(), "Escalated"));
        await _ticketRepository.UpdateAsync(row.Ticket);
        return Result.Success(await ToDetailResponseAsync(row));
    }

    public async Task<Result<AdminSupportTicketDetailResponse>> ResolveAsync(Guid ticketId, ResolveSupportTicketRequest request)
    {
        var row = await _ticketRepository.GetAdminRowByIdAsync(ticketId);
        if (row is null)
        {
            return Error.NotFound("SupportTicket.NotFound", "The specified ticket does not exist.");
        }

        try
        {
            row.Ticket.ChangeStatus(SupportTicketStatus.Resolved, request.ResolutionSummary);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Business("SupportTicket.CannotResolve", ex.Message);
        }

        await _auditLogWriter.WriteAsync(new AuditEntry("SupportTicket", ticketId.ToString(), "Resolved"));
        await _ticketRepository.UpdateAsync(row.Ticket);
        return Result.Success(await ToDetailResponseAsync(row));
    }

    public async Task<Result<AdminSupportTicketDetailResponse>> CloseAsync(Guid ticketId)
    {
        var row = await _ticketRepository.GetAdminRowByIdAsync(ticketId);
        if (row is null)
        {
            return Error.NotFound("SupportTicket.NotFound", "The specified ticket does not exist.");
        }

        try
        {
            row.Ticket.ChangeStatus(SupportTicketStatus.Closed);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Business("SupportTicket.CannotClose", ex.Message);
        }

        await _auditLogWriter.WriteAsync(new AuditEntry("SupportTicket", ticketId.ToString(), "Closed"));
        await _ticketRepository.UpdateAsync(row.Ticket);
        return Result.Success(await ToDetailResponseAsync(row));
    }

    /// <summary>
    /// Attaches a booking to the ticket (task 120e). Only a booking belonging
    /// to the ticket's own customer may be linked - same ownership check
    /// <see cref="Nestly.Infrastructure.Services.SupportTicketService.CreateAsync"/>
    /// applies at creation time, kept consistent here for a post-creation
    /// correction.
    /// </summary>
    public async Task<Result<AdminSupportTicketDetailResponse>> LinkBookingAsync(Guid ticketId, LinkSupportTicketBookingRequest request)
    {
        var row = await _ticketRepository.GetAdminRowByIdAsync(ticketId);
        if (row is null)
        {
            return Error.NotFound("SupportTicket.NotFound", "The specified ticket does not exist.");
        }

        var booking = await _bookingRepository.GetByIdAsync(request.BookingId);
        if (booking is null || booking.CustomerId != row.Ticket.CustomerId)
        {
            return Error.NotFound("SupportTicket.BookingNotFound", "The specified booking does not exist for this ticket's customer.");
        }

        row.Ticket.LinkBooking(request.BookingId);
        await _auditLogWriter.WriteAsync(new AuditEntry("SupportTicket", ticketId.ToString(), "BookingLinked", NewValues: request.BookingId.ToString()));
        await _ticketRepository.UpdateAsync(row.Ticket);
        return Result.Success(await ToDetailResponseAsync(row));
    }

    /// <summary>
    /// Builds the admin detail response, resolving the linked booking's
    /// summary if the ticket has one (task 120e). No cancel/refund action is
    /// exposed here - there is no admin booking-management API in this
    /// codebase yet (BookingMgmt is a separate, not-yet-landed vertical).
    /// TODO(BookingMgmt): once an admin booking controller/service exists,
    /// surface cancel/refund shortcuts here (or have the frontend call it
    /// directly using the <see cref="LinkedBookingSummaryResponse.Id"/> below)
    /// instead of only showing the booking's read-only summary.
    /// </summary>
    private async Task<AdminSupportTicketDetailResponse> ToDetailResponseAsync(AdminSupportTicketRow row)
    {
        LinkedBookingSummaryResponse? bookingSummary = null;
        if (row.Ticket.BookingId is { } bookingId)
        {
            var booking = await _bookingRepository.GetByIdAsync(bookingId);
            if (booking is not null)
            {
                bookingSummary = new LinkedBookingSummaryResponse(
                    booking.Id, booking.Status, booking.CustomerNameSnapshot, booking.SlotDate,
                    booking.SlotStartTimeSnapshot, booking.SlotEndTimeSnapshot, booking.TotalPayableSnapshot);
            }
        }

        var ticket = row.Ticket;
        return new AdminSupportTicketDetailResponse(
            ticket.Id,
            ticket.CustomerId,
            row.CustomerName,
            ticket.BookingId,
            bookingSummary,
            ticket.Category,
            ticket.Priority,
            ticket.Subject,
            ticket.Description,
            ticket.Status,
            ticket.ResolutionSummary,
            ticket.IsDisputed,
            ticket.DisputeOutcome,
            ticket.AssignedAdminUserId,
            row.AssignedAdminName,
            ticket.AssignedAtUtc,
            ticket.EscalatedAtUtc,
            ticket.Comments
                .OrderBy(c => c.CreatedAt)
                .Select(c => new SupportTicketCommentResponse(c.Id, c.AuthorType, c.Comment, c.CreatedAt))
                .ToList(),
            ticket.CreatedAtUtc,
            ticket.UpdatedAtUtc);
    }

    private static AdminSupportTicketSummaryResponse ToSummary(AdminSupportTicketRow row) => new(
        row.Ticket.Id,
        row.Ticket.CustomerId,
        row.CustomerName,
        row.Ticket.BookingId,
        row.Ticket.Category,
        row.Ticket.Priority,
        row.Ticket.Subject,
        row.Ticket.Status,
        row.Ticket.IsDisputed,
        row.Ticket.AssignedAdminUserId,
        row.AssignedAdminName,
        row.Ticket.CreatedAtUtc,
        row.Ticket.UpdatedAtUtc);
}
