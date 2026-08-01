using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>Lifecycle of a payout batch (PARTNER.md Financial Domain "partner_payout").</summary>
public enum PartnerPayoutStatus
{
    Pending,
    Processing,
    Paid,
    Failed
}

/// <summary>
/// A payout batch owed to a partner for a period (PARTNER.md Financial
/// Domain "partner_payout": period_start/end, total_amount, status,
/// payout_reference). OPEN DECISIONS #3: v1 payouts are manual bank
/// transfers - an admin runs a batch, then records the bank transfer
/// reference by hand as the status advances; there is no payment-gateway
/// webhook driving these transitions.
/// </summary>
public class PartnerPayout : Entity<Guid>
{
    public Guid PartnerId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public decimal TotalAmount { get; private set; }
    public PartnerPayoutStatus Status { get; private set; }

    /// <summary>Free-text bank transfer reference, set by the admin once the transfer has actually been made (manual, not gateway-issued - OPEN DECISIONS #3).</summary>
    public string? PayoutReference { get; private set; }

    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    protected PartnerPayout() { }

    public PartnerPayout(Guid id, Guid partnerId, DateOnly periodStart, DateOnly periodEnd, decimal totalAmount)
        : base(id)
    {
        if (periodEnd < periodStart)
        {
            throw new ArgumentException("Payout period end cannot be before its start.", nameof(periodEnd));
        }

        if (totalAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAmount), "A payout must have a positive total amount.");
        }

        PartnerId = partnerId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        TotalAmount = totalAmount;
        Status = PartnerPayoutStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Admin marks the bank transfer as initiated (task 148).</summary>
    public void MarkProcessing()
    {
        EnsureTransition(PartnerPayoutStatus.Pending, PartnerPayoutStatus.Processing);
        Status = PartnerPayoutStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Admin records the completed manual bank transfer and its reference (task 148, OPEN DECISIONS #3).</summary>
    public void MarkPaid(string payoutReference)
    {
        EnsureTransition(PartnerPayoutStatus.Processing, PartnerPayoutStatus.Paid);
        PayoutReference = string.IsNullOrWhiteSpace(payoutReference)
            ? throw new ArgumentException("A payout reference is required to mark a payout paid.", nameof(payoutReference))
            : payoutReference;
        Status = PartnerPayoutStatus.Paid;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Admin records that the manual transfer failed (e.g. bad account details), so it can be retried.</summary>
    public void MarkFailed(string? notes)
    {
        if (Status is PartnerPayoutStatus.Paid or PartnerPayoutStatus.Failed)
        {
            throw new InvalidOperationException($"Cannot mark a {Status} payout as failed.");
        }

        Status = PartnerPayoutStatus.Failed;
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureTransition(PartnerPayoutStatus expectedFrom, PartnerPayoutStatus to)
    {
        if (Status != expectedFrom)
        {
            throw new InvalidOperationException($"Cannot move a payout from {Status} to {to} (expected {expectedFrom}).");
        }
    }
}
