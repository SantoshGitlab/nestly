using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Immutable record of a booking cancellation (SRS 11.14.2, 32.2, tasks
/// 80a-c). At most one exists per booking - eligibility
/// (<see cref="BookingLifecycle"/> must allow the transition to
/// <see cref="BookingStatus.CancelledByCustomer"/>/<see cref="BookingStatus.CancelledByAdmin"/>)
/// is enforced by <c>ICancellationService</c> before this is ever created,
/// and a cancelled booking can never be cancelled again, so a unique index
/// on <see cref="BookingId"/> (see <c>BookingCancellationConfiguration</c>)
/// is safe to enforce at the database level too.
/// </summary>
public class BookingCancellation : Entity<Guid>
{
    public Guid BookingId { get; private set; }
    public CancellationActor Actor { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public bool WithinFreeCancellationWindow { get; private set; }
    public decimal CancellationFeeAmount { get; private set; }
    public decimal RefundAmount { get; private set; }
    public RefundMethod? RefundMethod { get; private set; }
    public Guid? RefundTransactionId { get; private set; }
    public string? InternalNotes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    protected BookingCancellation() { }

    public BookingCancellation(
        Guid id,
        Guid bookingId,
        CancellationActor actor,
        string reason,
        bool withinFreeCancellationWindow,
        decimal cancellationFeeAmount,
        decimal refundAmount,
        RefundMethod? refundMethod = null,
        Guid? refundTransactionId = null,
        string? internalNotes = null)
        : base(id)
    {
        if (cancellationFeeAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cancellationFeeAmount), "Cancellation fee cannot be negative.");
        }

        if (refundAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(refundAmount), "Refund amount cannot be negative.");
        }

        BookingId = bookingId;
        Actor = actor;
        Reason = string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException("Cancellation reason is required.", nameof(reason))
            : reason;
        WithinFreeCancellationWindow = withinFreeCancellationWindow;
        CancellationFeeAmount = cancellationFeeAmount;
        RefundAmount = refundAmount;
        RefundMethod = refundMethod;
        RefundTransactionId = refundTransactionId;
        InternalNotes = internalNotes;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Links the refund raised for this cancellation once <c>IRefundService</c> has created it.</summary>
    public void AttachRefund(Guid refundTransactionId, RefundMethod method)
    {
        RefundTransactionId = refundTransactionId;
        RefundMethod = method;
    }
}
