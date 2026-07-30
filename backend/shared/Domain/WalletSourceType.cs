namespace Nestly.Domain;

/// <summary>
/// The kind of event that produced a <see cref="WalletLedgerEntry"/> (SRS
/// 14.5 - "every credit/debit must reference source event"). Paired with
/// <see cref="WalletLedgerEntry.SourceReferenceId"/> for the concrete row.
/// </summary>
public enum WalletSourceType
{
    /// <summary>Credited from a <see cref="RefundTransaction"/> settled to wallet.</summary>
    Refund,

    /// <summary>Promotional/goodwill credit with no source aggregate (SourceReferenceId is null).</summary>
    PromotionalCredit,

    /// <summary>Manual adjustment (support/admin correction), no source aggregate.</summary>
    ManualAdjustment
}
