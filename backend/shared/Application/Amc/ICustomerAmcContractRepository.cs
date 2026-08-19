using Nestly.Domain;

namespace Nestly.Application.Amc;

public interface ICustomerAmcContractRepository
{
    Task<CustomerAmcContract?> GetByIdAsync(Guid id);

    /// <summary>Loaded with its visit history - a contract detail view is never useful without knowing what was already redeemed against it.</summary>
    Task<CustomerAmcContract?> GetByIdWithVisitsAsync(Guid id);

    /// <summary>All of one customer's contracts (active and past), newest first - "My AMC contracts".</summary>
    Task<IReadOnlyList<CustomerAmcContract>> ListByCustomerAsync(Guid customerId);

    Task AddAsync(CustomerAmcContract contract);

    Task UpdateAsync(CustomerAmcContract contract);

    /// <summary>Admin search: filter by status, search by customer name/mobile, paged.</summary>
    Task<(IReadOnlyList<CustomerAmcContract> Items, int TotalCount)> SearchAsync(
        CustomerAmcContractStatus? status, string? customerSearch, int page, int pageSize);

    /// <summary>Every Active contract, for the renewal report's status tile counts and the scheduled expiry sweep.</summary>
    Task<IReadOnlyList<CustomerAmcContract>> ListAllForReportAsync();

    /// <summary>Active contracts whose <see cref="CustomerAmcContract.EndDateUtc"/> falls within the horizon, or that are already Exhausted - the renewal report's "needs a renewal conversation" list.</summary>
    Task<IReadOnlyList<CustomerAmcContract>> ListExpiringOrExhaustedAsync(DateTime horizonFromUtc, DateTime horizonToUtc);

    /// <summary>Active contracts whose term has already passed <paramref name="asOfUtc"/> - the scheduled sweep's input for calling <see cref="CustomerAmcContract.Expire"/>.</summary>
    Task<IReadOnlyList<CustomerAmcContract>> ListPastTermStillActiveAsync(DateTime asOfUtc);

    /// <summary>Active contracts whose term ends within the reminder window and haven't already been notified for it - mirrors <c>ICustomerSubscriptionRepository.ListExpiringSoonAsync</c>.</summary>
    Task<IReadOnlyList<CustomerAmcContract>> ListNeedingExpiringSoonNotificationAsync(DateTime asOfUtc, DateTime windowEndUtc);
}
