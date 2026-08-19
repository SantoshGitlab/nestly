using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ProviderServiceAreaRepository : IProviderServiceAreaRepository
{
    private readonly NestlyDbContext _context;

    public ProviderServiceAreaRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ProviderServiceArea>> GetByProviderAsync(Guid providerId) =>
        await _context.Set<ProviderServiceArea>()
            .Where(x => x.ProviderId == providerId)
            .ToListAsync();

    public async Task ReplaceForProviderAsync(Guid providerId, IReadOnlyList<ProviderServiceArea> areas)
    {
        var existing = await _context.Set<ProviderServiceArea>()
            .Where(x => x.ProviderId == providerId)
            .ToListAsync();
        _context.Set<ProviderServiceArea>().RemoveRange(existing);

        await _context.Set<ProviderServiceArea>().AddRangeAsync(areas);

        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ListActiveCityNamesByProviderAsync(IReadOnlyList<Guid> providerIds)
    {
        if (providerIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var rows = await (
            from area in _context.Set<ProviderServiceArea>()
            join city in _context.Set<City>() on area.CityId equals city.Id
            where area.IsActive && providerIds.Contains(area.ProviderId)
            select new { area.ProviderId, city.Name }
        ).Distinct().ToListAsync();

        return rows
            .GroupBy(x => x.ProviderId)
            .ToDictionary(g => g.Key, IReadOnlyList<string> (g) => g.Select(x => x.Name).OrderBy(name => name).ToList());
    }
}
