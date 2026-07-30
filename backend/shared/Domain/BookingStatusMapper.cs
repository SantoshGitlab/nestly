namespace Nestly.Domain;

/// <summary>Coarse grouping a booking's internal status falls into, for list filtering (SRS 11.13, task 60b).</summary>
public enum BookingStatusBucket
{
    Upcoming,
    Completed,
    Cancelled
}

/// <summary>
/// Maps the internal <see cref="BookingStatus"/> to what a customer sees
/// (SRS 13.2: "customer-visible status label may differ from internal
/// operational status", task 61) and to the coarse bucket the booking list
/// APIs filter by.
/// </summary>
public static class BookingStatusMapper
{
    private static readonly Dictionary<BookingStatus, string> Labels = new()
    {
        [BookingStatus.Initiated] = "Booking Started",
        [BookingStatus.PaymentPending] = "Awaiting Payment",
        [BookingStatus.PaymentFailed] = "Payment Failed",
        [BookingStatus.Confirmed] = "Confirmed",
        [BookingStatus.AwaitingFulfilment] = "Preparing Your Service",
        [BookingStatus.Assigned] = "Professional Assigned",
        [BookingStatus.InProgress] = "Service in Progress",
        [BookingStatus.Completed] = "Completed",
        [BookingStatus.CancelledByCustomer] = "Cancelled by You",
        [BookingStatus.CancelledByAdmin] = "Cancelled",
        [BookingStatus.Rescheduled] = "Rescheduled",
        [BookingStatus.RefundPending] = "Refund in Progress",
        [BookingStatus.Refunded] = "Refunded",
    };

    /// <summary>
    /// RefundPending/Refunded are bucketed as Cancelled: the schema does not
    /// currently distinguish a post-completion refund from a
    /// cancellation-triggered one (both reach RefundPending from different
    /// states in <see cref="BookingLifecycle"/>), and the cancellation path
    /// is the overwhelmingly more common one to reach a refund from.
    /// Revisit if post-completion refunds become common enough to need their
    /// own bucket.
    /// </summary>
    private static readonly Dictionary<BookingStatus, BookingStatusBucket> Buckets = new()
    {
        [BookingStatus.Initiated] = BookingStatusBucket.Upcoming,
        [BookingStatus.PaymentPending] = BookingStatusBucket.Upcoming,
        [BookingStatus.PaymentFailed] = BookingStatusBucket.Upcoming,
        [BookingStatus.Confirmed] = BookingStatusBucket.Upcoming,
        [BookingStatus.AwaitingFulfilment] = BookingStatusBucket.Upcoming,
        [BookingStatus.Assigned] = BookingStatusBucket.Upcoming,
        [BookingStatus.InProgress] = BookingStatusBucket.Upcoming,
        [BookingStatus.Rescheduled] = BookingStatusBucket.Upcoming,
        [BookingStatus.Completed] = BookingStatusBucket.Completed,
        [BookingStatus.CancelledByCustomer] = BookingStatusBucket.Cancelled,
        [BookingStatus.CancelledByAdmin] = BookingStatusBucket.Cancelled,
        [BookingStatus.RefundPending] = BookingStatusBucket.Cancelled,
        [BookingStatus.Refunded] = BookingStatusBucket.Cancelled,
    };

    private static readonly ILookup<BookingStatusBucket, BookingStatus> StatusesByBucket =
        Buckets.ToLookup(kv => kv.Value, kv => kv.Key);

    public static string LabelFor(BookingStatus status) => Labels[status];

    public static BookingStatusBucket BucketFor(BookingStatus status) => Buckets[status];

    /// <summary>Every status that falls into <paramref name="bucket"/> - what a list API's bucket filter expands to in a query.</summary>
    public static IReadOnlyList<BookingStatus> StatusesInBucket(BookingStatusBucket bucket) =>
        StatusesByBucket[bucket].ToList();
}
