using Nestly.Domain;

namespace Nestly.Application.PartnerManagement;

/// <summary>Admin-recorded manual adjustment to a partner's earning ledger (task 148) - a credit (e.g. a correction) or a debit (a penalty).</summary>
public sealed record RecordPartnerEarningAdjustmentRequest(
    PartnerEarningEntryType EntryType,
    decimal Amount,
    PartnerEarningSourceType SourceType,
    Guid? SourceReferenceId,
    string Description);

public sealed record PartnerEarningLedgerEntryResponse(
    Guid Id,
    Guid PartnerId,
    PartnerEarningEntryType EntryType,
    decimal Amount,
    decimal BalanceAfter,
    PartnerEarningSourceType SourceType,
    Guid? SourceReferenceId,
    string Description,
    DateTime CreatedAtUtc);

public sealed record PartnerEarningsSummaryResponse(
    Guid PartnerId,
    decimal CurrentBalance,
    IReadOnlyList<PartnerEarningLedgerEntryResponse> Entries);

/// <summary>Admin runs a payout batch for a partner over a period (PARTNER.md API surface "run payout batch", task 148). Sums the earning ledger for that period - no gateway call, OPEN DECISIONS #3.</summary>
public sealed record CreatePartnerPayoutRequest(DateOnly PeriodStart, DateOnly PeriodEnd);

public sealed record UpdatePartnerPayoutStatusRequest(PartnerPayoutStatus Status, string? PayoutReference, string? Notes);

public sealed record PartnerPayoutResponse(
    Guid Id,
    Guid PartnerId,
    string PartnerDisplayName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal TotalAmount,
    PartnerPayoutStatus Status,
    string? PayoutReference,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record PartnerPayoutSearchResponse(IReadOnlyList<PartnerPayoutResponse> Items, int TotalCount, int Page, int PageSize);
