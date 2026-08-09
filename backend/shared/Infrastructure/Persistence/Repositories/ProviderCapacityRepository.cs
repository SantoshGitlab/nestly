using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ProviderCapacityRepository : IProviderCapacityRepository
{
    private readonly NestlyDbContext _context;

    public ProviderCapacityRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<ProviderCapacity?> GetByProviderAsync(Guid providerId) =>
        _context.Set<ProviderCapacity>().AsNoTracking().FirstOrDefaultAsync(x => x.ProviderId == providerId);

    /// <summary>
    /// Creates the provider's capacity row on first write, otherwise updates
    /// the existing one in place - the unique index on <c>ProviderId</c>
    /// means a plain insert would 500 on the second call, and this is the
    /// only writer of this table so there is no concurrent-insert race to
    /// guard against beyond what the index itself already catches.
    /// </summary>
    public async Task UpsertAsync(ProviderCapacity capacity)
    {
        var existing = await _context.Set<ProviderCapacity>().FirstOrDefaultAsync(x => x.ProviderId == capacity.ProviderId);
        if (existing is null)
        {
            await _context.Set<ProviderCapacity>().AddAsync(capacity);
        }
        else
        {
            existing.SetLimits(capacity.MaxJobsPerDay, capacity.MaxJobsPerSlot);
        }

        await _context.SaveChangesAsync();
    }
}
