using Nestly.Domain;

namespace Nestly.Application.Cancellations;

/// <summary>Cancellation policy summary shown before the customer confirms (SRS 11.14.3).</summary>
public record CancellationPolicyResponse(
    bool IsEligible,
    string? IneligibilityReason,
    bool WithinFreeCancellationWindow,
    decimal CancellationFeeAmount,
    decimal RefundAmount,
    RefundMethod RefundMethod,
    decimal FreeCancellationWindowHours,
    decimal LateCancellationFeePercentage);

/// <summary>Customer-submitted cancellation request (SRS 11.14.2 - reason is always captured).</summary>
public record CancelBookingRequest(string Reason);

/// <summary>The refund/fee outcome of a completed cancellation (SRS 11.14.3, 24.6).</summary>
public record CancellationOutcomeResponse(
    Guid BookingId,
    BookingStatus BookingStatus,
    bool WithinFreeCancellationWindow,
    decimal CancellationFeeAmount,
    decimal RefundAmount,
    RefundStatus? RefundStatus,
    RefundMethod? RefundMethod,
    Guid? RefundTransactionId,
    DateTime CancelledAtUtc);
