using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ServiceFaqRepository : IServiceFaqRepository
{
    private readonly NestlyDbContext _context;

    public ServiceFaqRepository(NestlyDbContext context)
    {
        _context = context;
    }

    // ServiceFaq has no explicit sort-order column - ordering by Id keeps the
    // list deterministic across requests (Postgres makes no ordering
    // guarantee without an ORDER BY) without inventing a display-order field
    // nothing else in the schema asked for yet.
    public async Task<IReadOnlyList<ServiceFaq>> ListByServiceAsync(Guid serviceId) =>
        await _context.Set<ServiceFaq>()
            .Where(f => f.ServiceId == serviceId)
            .OrderBy(f => f.Id)
            .ToListAsync();
}
