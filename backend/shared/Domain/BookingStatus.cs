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
    Refunded
}
