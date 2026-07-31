using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Support;

/// <summary>
/// Formalizes the valid/invalid branch on a support ticket (SRS 11.18.1
/// "wrong charge / pricing dispute", task 155) into its own admin flow: mark
/// a ticket disputed, then resolve it as either a refund (valid) or a
/// close/rework (invalid), tracking the outcome either way.
///
/// Admin authorization/authentication does not exist yet (Phase 6) - see
/// <c>SupportTicketDisputesController</c>'s doc comment for how that gap is
/// handled at the API layer. This interface is the real capability Phase 6
/// will eventually gate; nothing about it changes when that lands.
/// </summary>
public interface IDisputeResolutionService
{
    /// <summary>Opens a formal dispute investigation on a ticket.</summary>
    Task<Result<SupportTicketDetailResponse>> MarkDisputedAsync(Guid ticketId);

    /// <summary>Resolves an open dispute - raising a refund against the linked booking when the outcome is RefundValid.</summary>
    Task<Result<DisputeResolutionResponse>> ResolveAsync(Guid ticketId, ResolveDisputeRequest request);
}
