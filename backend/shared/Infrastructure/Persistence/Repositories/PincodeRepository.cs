using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PincodeRepository : IPincodeRepository
{
    private readonly NestlyDbContext _context;

    public PincodeRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Pincode entity)
    {
        await _context.Set<Pincode>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Pincode entity)
    {
        _context.Set<Pincode>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<Pincode?> GetByIdAsync(Guid id) =>
        _context.Set<Pincode>().FirstOrDefaultAsync(p => p.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<Pincode>().AnyAsync(p => p.Id == id);

    public async Task<IReadOnlyList<Pincode>> ListAsync(Guid? cityId) =>
        await _context.Set<Pincode>()
            .Include(p => p.City)
            .Where(p => cityId == null || p.CityId == cityId)
            .OrderBy(p => p.Code)
            .ToListAsync();

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null) =>
        _context.Set<Pincode>().AnyAsync(p => p.Code == code && (excludeId == null || p.Id != excludeId));
}
