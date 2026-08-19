namespace Nestly.Domain;

/// <summary>
/// Where the money a <see cref="RefundTransaction"/> hands back originally
/// came from - as opposed to <see cref="RefundMethod"/>, which is where it is
/// handed back TO. The two are independent: a gateway payment can be refunded
/// as wallet credit (a goodwill settlement), but wallet credit the customer
/// spent at checkout can only ever go back to the wallet, since it never
/// passed through a gateway that could reverse it.
///
/// This is the discriminator that lets <see cref="RefundTransaction.PaymentTransactionId"/>
/// be null: a booking can be settled entirely from wallet balance (SRS 11.7.2,
/// task 310) or from a 100%-off coupon/AMC entitlement and be confirmed with
/// no PaymentTransaction row at all (task 331), and the wallet half of such a
/// booking still has to be refundable.
/// </summary>
public enum RefundFundingSource
{
    /// <summary>Refunds part or all of the booking's gateway <see cref="PaymentTransaction"/>.</summary>
    Payment,

    /// <summary>Refunds part or all of the wallet balance the booking consumed at checkout (<see cref="Booking.WalletCreditAppliedSnapshot"/>).</summary>
    Wallet
}
