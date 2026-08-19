using Microsoft.EntityFrameworkCore;
using Nestly.Application.Amc;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CustomerAmcContractRepository : ICustomerAmcContractRepository
{
    private readonly NestlyDbContext _context;

    public CustomerAmcContractRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<CustomerAmcContract?> GetByIdAsync(Guid id) =>
        _context.CustomerAmcContracts.FirstOrDefaultAsync(c => c.Id == id);

    public Task<CustomerAmcContract?> GetByIdWithVisitsAsync(Guid id) =>
        // AmcServiceVisit is loaded separately below via IAmcServiceVisitRepository,
        // not as a navigation here - the contract entity itself carries no
        // collection of visits (see AmcServiceVisit's doc comment on why it
        // is modeled as its own repository, not a navigation property).
        _context.CustomerAmcContracts.FirstOrDefaultAsync(c => c.Id == id);

    public async Task<IReadOnlyList<CustomerAmcContract>> ListByCustomerAsync(Guid customerId) =>
        await _context.CustomerAmcContracts
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync();

    public async Task AddAsync(CustomerAmcContract contract)
    {
        await _context.CustomerAmcContracts.AddAsync(contract);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CustomerAmcContract contract)
    {
        _context.CustomerAmcContracts.Update(contract);
        await _context.SaveChangesAsync();
    }

    public async Task<(IReadOnlyList<CustomerAmcContract> Items, int TotalCount)> SearchAsync(
        CustomerAmcContractStatus? status, string? customerSearch, int page, int pageSize)
    {
        var query = _context.CustomerAmcContracts.AsQueryable();

        if (status is { } s)
        {
            query = query.Where(c => c.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(customerSearch))
        {
            var term = customerSearch.Trim();
            query = query.Where(c => _context.Set<Customer>().Any(cu =>
                cu.Id == c.CustomerId && (cu.Name.Contains(term) || cu.Mobile.Contains(term))));
        }

        int total = await query.CountAsync();

        var items = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<IReadOnlyList<CustomerAmcContract>> ListAllForReportAsync() =>
        await _context.CustomerAmcContracts.ToListAsync();

    public async Task<IReadOnlyList<CustomerAmcContract>> ListExpiringOrExhaustedAsync(DateTime horizonFromUtc, DateTime horizonToUtc) =>
        await _context.CustomerAmcContracts
            .Where(c =>
                (c.Status == CustomerAmcContractStatus.Active && c.EndDateUtc >= horizonFromUtc && c.EndDateUtc <= horizonToUtc)
                || c.Status == CustomerAmcContractStatus.Exhausted)
            .OrderBy(c => c.EndDateUtc)
            .ToListAsync();

    public async Task<IReadOnlyList<CustomerAmcContract>> ListPastTermStillActiveAsync(DateTime asOfUtc) =>
        await _context.CustomerAmcContracts
            .Where(c => c.Status == CustomerAmcContractStatus.Active && c.EndDateUtc < asOfUtc)
            .ToListAsync();

    public async Task<IReadOnlyList<CustomerAmcContract>> ListNeedingExpiringSoonNotificationAsync(DateTime asOfUtc, DateTime windowEndUtc) =>
        await _context.CustomerAmcContracts
            .Where(c => c.Status == CustomerAmcContractStatus.Active
                && c.EndDateUtc > asOfUtc
                && c.EndDateUtc <= windowEndUtc
                && c.ExpiringSoonNotifiedForEndDateUtc != c.EndDateUtc)
            .ToListAsync();
}
