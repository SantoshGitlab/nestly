using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PartnerAuthIdentityRepository : IPartnerAuthIdentityRepository
{
    private readonly NestlyDbContext _context;

    public PartnerAuthIdentityRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PartnerAuthIdentity entity)
    {
        await _context.Set<PartnerAuthIdentity>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(PartnerAuthIdentity entity)
    {
        _context.Set<PartnerAuthIdentity>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<PartnerAuthIdentity?> GetByProviderAsync(AuthProviderType provider, string identifier) =>
        _context.Set<PartnerAuthIdentity>()
            .FirstOrDefaultAsync(x => x.Provider == provider && x.Identifier == identifier);

    public async Task<IReadOnlyList<PartnerAuthIdentity>> GetByPartnerAsync(Guid partnerId) =>
        await _context.Set<PartnerAuthIdentity>()
            .Where(x => x.PartnerId == partnerId)
            .ToListAsync();

    public Task<bool> ExistsAsync(AuthProviderType provider, string identifier) =>
        _context.Set<PartnerAuthIdentity>().AnyAsync(x => x.Provider == provider && x.Identifier == identifier);
}
