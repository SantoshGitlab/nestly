using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CustomerSessionRepository : ICustomerSessionRepository
{
    private readonly NestlyDbContext _context;

    public CustomerSessionRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CustomerSession entity)
    {
        await _context.Set<CustomerSession>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CustomerSession entity)
    {
        _context.Set<CustomerSession>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<CustomerSession?> GetByRefreshTokenHashAsync(string refreshTokenHash) =>
        _context.Set<CustomerSession>().FirstOrDefaultAsync(s => s.RefreshTokenHash == refreshTokenHash);

    public async Task<int> RevokeAllForCustomerAsync(Guid customerId)
    {
        var now = DateTime.UtcNow;

        // Filtered on the same predicate as CustomerSession.IsActive so an
        // already-revoked or already-expired row is not touched (and its
        // original RevokedAt timestamp is not overwritten).
        var active = await _context.Set<CustomerSession>()
            .Where(s => s.CustomerId == customerId && s.RevokedAt == null && s.ExpiresAt > now)
            .ToListAsync();

        if (active.Count == 0)
        {
            return 0;
        }

        foreach (var session in active)
        {
            session.Revoke();
        }

        await _context.SaveChangesAsync();
        return active.Count;
    }
}
