using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PartnerSessionRepository : IPartnerSessionRepository
{
    private readonly NestlyDbContext _context;

    public PartnerSessionRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PartnerSession entity)
    {
        await _context.Set<PartnerSession>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(PartnerSession entity)
    {
        _context.Set<PartnerSession>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<PartnerSession?> GetByRefreshTokenHashAsync(string refreshTokenHash) =>
        _context.Set<PartnerSession>().FirstOrDefaultAsync(s => s.RefreshTokenHash == refreshTokenHash);

    public async Task<int> RevokeAllForPartnerAsync(Guid partnerId)
    {
        var now = DateTime.UtcNow;

        var active = await _context.Set<PartnerSession>()
            .Where(s => s.PartnerId == partnerId && s.RevokedAt == null && s.ExpiresAt > now)
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
