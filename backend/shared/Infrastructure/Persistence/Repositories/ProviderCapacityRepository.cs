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
}
