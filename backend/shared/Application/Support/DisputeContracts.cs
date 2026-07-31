using Nestly.Domain;

namespace Nestly.Application.Support;

/// <summary>Admin decision on a disputed ticket (task 155). RefundAmount is only meaningful when Outcome is RefundValid - null refunds whatever remains on the linked booking's payment in full.</summary>
public record ResolveDisputeRequest(DisputeResolutionOutcome Outcome, string ResolutionSummary, decimal? RefundAmount);

/// <summary>Outcome of resolving a dispute, including the refund it raised if any (task 155).</summary>
public record DisputeResolutionResponse(
    Guid TicketId,
    SupportTicketStatus Status,
    bool IsDisputed,
    DisputeResolutionOutcome? DisputeOutcome,
    string? ResolutionSummary,
    Guid? RefundTransactionId,
    RefundStatus? RefundStatus,
    DateTime? DisputeResolvedAtUtc);
