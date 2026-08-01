using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PartnerLoginAttemptRepository : IPartnerLoginAttemptRepository
{
    private readonly NestlyDbContext _context;

    public PartnerLoginAttemptRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PartnerLoginAttempt entity)
    {
        await _context.Set<PartnerLoginAttempt>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public Task<int> CountFailuresSinceAsync(string identifier, DateTime sinceUtc) =>
        _context.Set<PartnerLoginAttempt>()
            .CountAsync(a => a.Identifier == identifier && !a.Succeeded && a.OccurredAtUtc >= sinceUtc);
}
