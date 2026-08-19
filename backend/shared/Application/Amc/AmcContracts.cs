using Nestly.Domain;

namespace Nestly.Application.Amc;

// ---- Plan catalog (admin) ----

/// <summary>Admin plan detail/list row.</summary>
public sealed record AmcPlanAdminResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    decimal Price,
    int TermMonths,
    int VisitsIncluded,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>Admin request to create a plan. Starts active - see <see cref="AmcPlan"/>'s constructor.</summary>
public sealed record AmcPlanCreateRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    int TermMonths,
    int VisitsIncluded);

/// <summary>Admin request to edit every mutable field of an existing plan. Existing contracts are unaffected - see <see cref="AmcPlan.Update"/>.</summary>
public sealed record AmcPlanUpdateRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    int TermMonths,
    int VisitsIncluded);

// ---- Plan browse (customer) ----

/// <summary>One browsable plan - the public subset of <see cref="AmcPlan"/>, omitting admin-only bookkeeping.</summary>
public sealed record AmcPlanBrowseResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    decimal Price,
    int TermMonths,
    int VisitsIncluded);

// ---- Purchase / contract (customer) ----

public sealed record AmcContractPurchaseRequest(Guid PlanId, string AssetLabel);

/// <summary>One AMC service visit's audit row, part of a contract's history.</summary>
public sealed record AmcServiceVisitResponse(Guid Id, Guid BookingId, DateTime ConsumedAtUtc);

/// <summary>"My AMC contract" - every field a holder needs to see, drawn from the contract's own snapshot, never a live join back to the plan (see <see cref="CustomerAmcContract"/>'s doc comment).</summary>
public sealed record MyAmcContractResponse(
    Guid Id,
    string PlanName,
    string CategoryName,
    decimal Price,
    int TermMonths,
    int VisitsIncluded,
    string AssetLabel,
    CustomerAmcContractStatus Status,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    int VisitsRemaining,
    bool CanRedeemNow,
    DateTime CreatedAtUtc,
    DateTime? CancelledAtUtc,
    IReadOnlyList<AmcServiceVisitResponse> Visits);

// ---- Admin contract list/report ----

public sealed record AmcContractAdminListItemResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string PlanName,
    string AssetLabel,
    CustomerAmcContractStatus Status,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    int VisitsIncluded,
    int VisitsRemaining,
    DateTime CreatedAtUtc);

public sealed record AmcContractAdminSearchResponse(
    IReadOnlyList<AmcContractAdminListItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AmcContractStatusCount(CustomerAmcContractStatus Status, int ContractCount);

/// <summary>
/// The admin renewal-pipeline report (docs/AMC.md's justification for the
/// module - "best cash-flow profile in the catalogue" only realizes if
/// expiring/exhausted contracts actually get renewed). Mirrors the
/// aggregate-tiles-plus-list shape <c>RecurringPlanReport</c> already
/// established.
/// </summary>
public sealed record AmcRenewalReportResponse(
    int TotalContracts,
    IReadOnlyList<AmcContractStatusCount> ByStatus,
    DateTime HorizonFromUtc,
    DateTime HorizonToUtc,
    int ExpiringInHorizon,
    int ExhaustedInHorizon,
    IReadOnlyList<AmcContractAdminListItemResponse> ExpiringOrExhaustedContracts);
