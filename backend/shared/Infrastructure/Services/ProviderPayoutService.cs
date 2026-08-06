using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.ProviderManagement;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="IProviderPayoutService"/>
/// <remarks>
/// Writes an audit entry for batch creation and every status change (task
/// 132c gap fix, NESTLY-007): a payout batch and its Processing/Paid/Failed
/// transitions are directly financial (bank reference, amount owed), the
/// same "every write is audited" reasoning <c>CouponManagementService</c>'s
/// doc comment gives for discount changes applies here. Staged before the
/// repository call so the repository's own <c>SaveChangesAsync</c> commits
/// both in one transaction.
/// </remarks>
public class ProviderPayoutService : IProviderPayoutService
{
    /// <summary>
    /// Task 251: page-size bounds for <see cref="SearchAsync"/>. Clamped here
    /// rather than in each caller because both payout list endpoints - admin
    /// PayoutsController.Search and provider EarningsController.ListPayouts -
    /// funnel through this one method, and neither validates its query
    /// string. Unbounded, a single request materializes the whole table;
    /// a page below 1 reaches the repository as a negative OFFSET, which
    /// PostgreSQL rejects outright ("OFFSET must not be negative") for a 500.
    /// Same limits as AuditLogQueryService and the admin *Validators.cs.
    /// </summary>
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IProviderRepository _providerRepository;
    private readonly IProviderPayoutRepository _payoutRepository;
    private readonly IProviderEarningLedgerRepository _ledgerRepository;
    private readonly IAuditLogWriter _auditLogWriter;

    public ProviderPayoutService(
        IProviderRepository providerRepository,
        IProviderPayoutRepository payoutRepository,
        IProviderEarningLedgerRepository ledgerRepository,
        IAuditLogWriter auditLogWriter)
    {
        _providerRepository = providerRepository;
        _payoutRepository = payoutRepository;
        _ledgerRepository = ledgerRepository;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<Result<ProviderPayoutResponse>> CreateBatchAsync(Guid providerId, CreateProviderPayoutRequest request)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider is null)
        {
            return Error.NotFound("ProviderPayout.ProviderNotFound", "Provider was not found.");
        }

        var entries = await _ledgerRepository.ListByProviderAndPeriodAsync(providerId, request.PeriodStart, request.PeriodEnd);
        decimal total = entries.Sum(e => e.EntryType == ProviderEarningEntryType.Credit ? e.Amount : -e.Amount);

        if (total <= 0)
        {
            return Error.Business("ProviderPayout.NothingToPay", "There is no positive earning balance for this provider in the given period.");
        }

        var payout = new ProviderPayout(Guid.NewGuid(), providerId, request.PeriodStart, request.PeriodEnd, total);

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ProviderPayout",
            payout.Id.ToString(),
            "Created",
            NewValues: $"ProviderId={providerId}; Status=(none)->{payout.Status}; TotalAmount={payout.TotalAmount}"));

        await _payoutRepository.AddAsync(payout);

        return ToResponse(payout, provider.DisplayName);
    }

    public async Task<Result<ProviderPayoutResponse>> GetByIdAsync(Guid payoutId)
    {
        var payout = await _payoutRepository.GetByIdAsync(payoutId);
        if (payout is null)
        {
            return Error.NotFound("ProviderPayout.NotFound", "Payout was not found.");
        }

        var provider = await _providerRepository.GetByIdAsync(payout.ProviderId);
        return ToResponse(payout, provider?.DisplayName ?? "(unknown provider)");
    }

    public async Task<Result<ProviderPayoutSearchResponse>> SearchAsync(Guid? providerId, ProviderPayoutStatus? status, int page, int pageSize)
    {
        // Clamp before the query, and echo the clamped values back in the
        // response so a caller that asked for page 0 / pageSize 10000 can see
        // what it actually got rather than silently mis-paging.
        page = page < 1 ? 1 : page;
        pageSize = pageSize switch
        {
            <= 0 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize
        };

        var (rows, totalCount) = await _payoutRepository.SearchAsync(providerId, status, page, pageSize);

        // Task 254: the local dictionary only avoided re-querying a provider
        // already seen on this page - the first row for each distinct provider
        // still cost its own round trip, so an admin page spanning 100
        // providers issued 100 of them. One batched lookup instead.
        var displayNames = await _providerRepository.GetDisplayNamesByIdsAsync(
            rows.Select(p => p.ProviderId).Distinct().ToList());

        var items = rows
            .Select(payout => ToResponse(payout, displayNames.GetValueOrDefault(payout.ProviderId, "(unknown provider)")))
            .ToList();

        return new ProviderPayoutSearchResponse(items, totalCount, page, pageSize);
    }

    public async Task<Result<ProviderPayoutResponse>> UpdateStatusAsync(Guid payoutId, UpdateProviderPayoutStatusRequest request)
    {
        var payout = await _payoutRepository.GetByIdAsync(payoutId);
        if (payout is null)
        {
            return Error.NotFound("ProviderPayout.NotFound", "Payout was not found.");
        }

        var previousStatus = payout.Status;

        try
        {
            switch (request.Status)
            {
                case ProviderPayoutStatus.Processing:
                    payout.MarkProcessing();
                    break;
                case ProviderPayoutStatus.Paid:
                    if (string.IsNullOrWhiteSpace(request.PayoutReference))
                    {
                        return Error.Validation("ProviderPayout.ReferenceRequired", "A payout reference is required to mark a payout paid.");
                    }

                    payout.MarkPaid(request.PayoutReference);
                    break;
                case ProviderPayoutStatus.Failed:
                    payout.MarkFailed(request.Notes);
                    break;
                default:
                    return Error.Validation("ProviderPayout.InvalidTargetStatus", "A payout can only be moved to Processing, Paid or Failed.");
            }
        }
        catch (InvalidOperationException ex)
        {
            return Error.Business("ProviderPayout.InvalidTransition", ex.Message);
        }

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ProviderPayout",
            payout.Id.ToString(),
            "StatusChanged",
            NewValues: $"ProviderId={payout.ProviderId}; Status={previousStatus}->{payout.Status}; PayoutReference={payout.PayoutReference ?? "null"}"));

        await _payoutRepository.UpdateAsync(payout);

        var provider = await _providerRepository.GetByIdAsync(payout.ProviderId);
        return ToResponse(payout, provider?.DisplayName ?? "(unknown provider)");
    }

    private static ProviderPayoutResponse ToResponse(ProviderPayout payout, string providerDisplayName) => new(
        payout.Id, payout.ProviderId, providerDisplayName, payout.PeriodStart, payout.PeriodEnd,
        payout.TotalAmount, payout.Status, payout.PayoutReference, payout.Notes, payout.CreatedAt, payout.UpdatedAt);
}
