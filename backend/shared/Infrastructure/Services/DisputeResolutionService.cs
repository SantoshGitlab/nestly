using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Refunds;
using Nestly.Application.Support;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Dispute mark/resolve workflow on a support ticket (SRS 11.18.1, task 155).
/// A RefundValid resolution reuses Phase 4's <see cref="IRefundService"/> to
/// actually raise the refund against the ticket's linked booking - the same
/// engine <see cref="CancellationService"/> uses - rather than duplicating
/// gateway/wallet handling here.
/// </summary>
/// <remarks>
/// Writes an audit entry for every resolution (task 132c gap fix,
/// NESTLY-007): a resolved dispute can move real money via
/// <see cref="IRefundService"/>, the same "every write is audited" reasoning
/// <c>CouponManagementService</c>'s doc comment gives for discount changes
/// applies doubly here. Staged before the repository call so the repository's
/// own <c>SaveChangesAsync</c> commits both in one transaction.
/// </remarks>
public class DisputeResolutionService : IDisputeResolutionService
{
    private readonly ISupportTicketRepository _ticketRepository;
    private readonly IRefundService _refundService;
    private readonly IAuditLogWriter _auditLogWriter;

    public DisputeResolutionService(ISupportTicketRepository ticketRepository, IRefundService refundService, IAuditLogWriter auditLogWriter)
    {
        _ticketRepository = ticketRepository;
        _refundService = refundService;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<Result<SupportTicketDetailResponse>> MarkDisputedAsync(Guid ticketId)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId);
        if (ticket is null)
        {
            return Error.NotFound("Dispute.TicketNotFound", "The specified ticket does not exist.");
        }

        try
        {
            ticket.MarkDisputed();
        }
        catch (InvalidOperationException ex)
        {
            return Error.Business("Dispute.CannotOpen", ex.Message);
        }

        await _ticketRepository.UpdateAsync(ticket);
        return Result.Success(ToDetailResponse(ticket));
    }

    public async Task<Result<DisputeResolutionResponse>> ResolveAsync(Guid ticketId, ResolveDisputeRequest request)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId);
        if (ticket is null)
        {
            return Error.NotFound("Dispute.TicketNotFound", "The specified ticket does not exist.");
        }

        if (!ticket.IsDisputed)
        {
            return Error.Business("Dispute.NotOpen", "This ticket has no open dispute to resolve - call MarkDisputed first.");
        }

        // NESTLY-003: hoisted ahead of the refund branch below. IsDisputed is
        // set once by MarkDisputed and never cleared by ResolveDispute, so it
        // stays true on a ticket whose dispute was already resolved - without
        // this check a second ResolveAsync call would reach
        // _refundService.InitiatePartialRefundAsync and fire a real second
        // payout before ticket.ResolveDispute's ChangeStatus(Resolved) rejects
        // the Resolved -> Resolved transition further down, turning a
        // real money-moving success into a reported failure.
        if (!SupportTicketLifecycle.IsValidTransition(ticket.Status, SupportTicketStatus.Resolved))
        {
            return Error.Business("Dispute.CannotResolve", $"Cannot resolve a dispute on a ticket that is already {ticket.Status}.");
        }

        Guid? refundTransactionId = null;
        RefundStatus? refundStatus = null;

        if (request.Outcome == DisputeResolutionOutcome.RefundValid)
        {
            if (ticket.BookingId is not { } bookingId)
            {
                return Error.Validation("Dispute.NoLinkedBooking", "A refund cannot be issued for a dispute with no linked booking.");
            }

            var refundResult = request.RefundAmount is { } amount
                ? await _refundService.InitiatePartialRefundAsync(bookingId, amount, request.ResolutionSummary)
                : await _refundService.InitiateFullRefundAsync(bookingId, request.ResolutionSummary);

            if (refundResult.IsFailure)
            {
                return refundResult.Error;
            }

            refundTransactionId = refundResult.Value.Id;
            refundStatus = refundResult.Value.Status;
        }

        try
        {
            ticket.ResolveDispute(request.Outcome, request.ResolutionSummary);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Business("Dispute.CannotResolve", ex.Message);
        }

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "SupportTicket",
            ticket.Id.ToString(),
            "DisputeResolved",
            NewValues: $"Outcome={request.Outcome}; RefundAmount={request.RefundAmount?.ToString() ?? "null"}; RefundTransactionId={refundTransactionId?.ToString() ?? "null"}"));

        await _ticketRepository.UpdateAsync(ticket);

        return Result.Success(new DisputeResolutionResponse(
            ticket.Id, ticket.Status, ticket.IsDisputed, ticket.DisputeOutcome, ticket.ResolutionSummary,
            refundTransactionId, refundStatus, ticket.DisputeResolvedAtUtc));
    }

    private static SupportTicketDetailResponse ToDetailResponse(SupportTicket ticket) => new(
        ticket.Id,
        ticket.CustomerId,
        ticket.BookingId,
        ticket.Category,
        ticket.Priority,
        ticket.Subject,
        ticket.Description,
        ticket.Status,
        ticket.ResolutionSummary,
        ticket.IsDisputed,
        ticket.DisputeOutcome,
        ticket.Comments
            .OrderBy(c => c.CreatedAt)
            .Select(c => new SupportTicketCommentResponse(c.Id, c.AuthorType, c.Comment, c.CreatedAt))
            .ToList(),
        ticket.CreatedAtUtc,
        ticket.UpdatedAtUtc);
}
