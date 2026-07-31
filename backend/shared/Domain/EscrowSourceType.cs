namespace Nestly.Domain;

/// <summary>
/// The kind of event that produced a <see cref="PlatformEscrowLedger"/> entry
/// (task 158) - mirrors <see cref="WalletSourceType"/>'s traceability role.
/// Paired with <see cref="PlatformEscrowLedger.SourceReferenceId"/> for the
/// concrete row.
/// </summary>
public enum EscrowSourceType
{
    /// <summary>Hold: a customer's payment succeeded (SourceReferenceId is the PaymentTransactionId).</summary>
    PaymentConfirmed,

    /// <summary>Release: the booking reached BookingStatus.Completed and its hold was paid out to the provider net of commission (SourceReferenceId is the PaymentTransactionId).</summary>
    BookingCompleted,

    /// <summary>Release: a refund was issued against the booking's payment, so its (remaining) hold is released back out instead of to a provider (SourceReferenceId is the RefundTransactionId).</summary>
    RefundIssued
}
