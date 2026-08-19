using Microsoft.EntityFrameworkCore;
using Nestly.Application.Geography;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class GeographyRepository : IGeographyRepository
{
    private const int LocalitySearchLimit = 20;

    private readonly NestlyDbContext _context;

    public GeographyRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<bool> CityExistsAsync(Guid cityId) =>
        _context.Set<City>().AnyAsync(c => c.Id == cityId && c.IsActive);

    public async Task<IReadOnlyList<CityResponse>> ListActiveCitiesAsync() =>
        await (
            from city in _context.Set<City>()
            join state in _context.Set<State>() on city.StateId equals state.Id
            where city.IsActive && state.IsActive
            orderby state.Name, city.Name
            select new CityResponse(city.Id, city.Name, state.Name)
        ).ToListAsync();

    public async Task<IReadOnlyList<LocalityResponse>> SearchActiveLocalitiesAsync(Guid cityId, string? search)
    {
        var query =
            from locality in _context.Set<Locality>()
            join zone in _context.Set<Zone>() on locality.ZoneId equals zone.Id
            join pincode in _context.Set<Pincode>() on locality.PincodeId equals pincode.Id
            where zone.CityId == cityId && locality.IsActive && zone.IsActive && pincode.IsActive
            select new { locality, zone, pincode };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLower();
            query = query.Where(x =>
                x.locality.Name.ToLower().Contains(normalized) ||
                x.pincode.Code.Contains(normalized));
        }

        return await query
            .OrderBy(x => x.locality.Name)
            .Take(LocalitySearchLimit)
            .Select(x => new LocalityResponse(x.locality.Id, x.locality.Name, x.zone.Name, x.pincode.Code, x.pincode.Id))
            .ToListAsync();
    }

    public async Task<Guid?> FindActivePincodeIdByCodeAsync(string pincodeCode)
    {
        var match = await _context.Set<Pincode>()
            .Where(p => p.IsActive && p.Code == pincodeCode)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync();

        return match;
    }

    public Task<PincodeLookupResponse?> ResolvePincodeLocationAsync(string pincodeCode) =>
        (
            from pincode in _context.Set<Pincode>()
            join city in _context.Set<City>() on pincode.CityId equals city.Id
            join state in _context.Set<State>() on city.StateId equals state.Id
            where pincode.IsActive && city.IsActive && state.IsActive && pincode.Code == pincodeCode
            select new PincodeLookupResponse(city.Id, city.Name, state.Name)
        ).FirstOrDefaultAsync();
}
