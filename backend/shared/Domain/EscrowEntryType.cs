namespace Nestly.Domain;

/// <summary>Direction of a <see cref="PlatformEscrowLedger"/> entry (task 158).</summary>
public enum EscrowEntryType
{
    /// <summary>Funds moved into escrow - a customer's payment being held pending fulfilment.</summary>
    Hold,

    /// <summary>Funds moved out of escrow - either released to the provider on completion, or released back out because the booking was refunded.</summary>
    Release
}
