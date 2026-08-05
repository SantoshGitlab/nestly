namespace Nestly.Domain;

/// <summary>Booking lifecycle states (SRS 13.1).</summary>
public enum BookingStatus
{
    Initiated,
    PaymentPending,
    PaymentFailed,
    Confirmed,
    AwaitingFulfilment,
    Assigned,
    InProgress,
    Completed,
    CancelledByCustomer,
    CancelledByAdmin,
    Rescheduled,
    RefundPending,
    Refunded,

    /// <summary>Task 240: a PaymentPending booking abandoned past the expiry window swept by BookingExpirySweepJob - never paid for, so distinct from CancelledByCustomer/CancelledByAdmin, which carry cancellation-fee/refund semantics that never applied here.</summary>
    Expired
}
