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
    Expired,

    // Tasks 264: the two fulfilment states end-to-end order tracking needs.
    // Appended here rather than inserted between Assigned and InProgress
    // where they belong chronologically: neither API registers a
    // JsonStringEnumConverter, so this enum crosses the wire as its ordinal
    // and the three web clients mirror those ordinals by hand
    // (customer-web/admin-web src/lib/types.ts). Inserting mid-enum would
    // renumber Assigned onwards and silently remap every already-deployed
    // client's booking status. Declaration order carries no lifecycle
    // meaning - BookingLifecycle's table is the sole authority on ordering.

    /// <summary>The assigned provider is travelling to the customer's address - the "on the way" state a customer can be shown before anyone has arrived.</summary>
    ProviderEnRoute,

    /// <summary>The assigned provider has reached the address but has not begun the work yet - previously conflated into <see cref="InProgress"/> by IProviderJobService.StartAsync.</summary>
    ProviderArrived
}
