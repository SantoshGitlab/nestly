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
public class DisputeResolutionService : IDisputeResolutionService
{
    private readonly ISupportTicketRepository _ticketRepository;
    private readonly IRefundService _refundService;

    public DisputeResolutionService(ISupportTicketRepository ticketRepository, IRefundService refundService)
    {
        _ticketRepository = ticketRepository;
        _refundService = refundService;
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
