using Microsoft.EntityFrameworkCore;
using Nestly.Application.NestlyCoins;
using Nestly.Domain.NestlyCoins;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class NestlyCoinsProgramConfigRepository : INestlyCoinsProgramConfigRepository
{
    private readonly NestlyDbContext _context;

    public NestlyCoinsProgramConfigRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<NestlyCoinsProgramConfig?> GetByAudienceAsync(NestlyCoinsAudience audience) =>
        _context.NestlyCoinsProgramConfigs.FirstOrDefaultAsync(c => c.Audience == audience);

    public async Task<IReadOnlyList<NestlyCoinsProgramConfig>> ListAsync() =>
        await _context.NestlyCoinsProgramConfigs.OrderBy(c => c.Audience).ToListAsync();

    public async Task AddAsync(NestlyCoinsProgramConfig config)
    {
        await _context.NestlyCoinsProgramConfigs.AddAsync(config);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(NestlyCoinsProgramConfig config)
    {
        if (_context.Entry(config).State == EntityState.Detached)
        {
            _context.NestlyCoinsProgramConfigs.Update(config);
        }

        await _context.SaveChangesAsync();
    }
}
