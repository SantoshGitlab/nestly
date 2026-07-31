using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PartnerRepository : IPartnerRepository
{
    private readonly NestlyDbContext _context;

    public PartnerRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Partner entity)
    {
        await _context.Set<Partner>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Partner entity)
    {
        _context.Set<Partner>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<Partner?> GetByIdAsync(Guid id) =>
        _context.Set<Partner>().FirstOrDefaultAsync(p => p.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<Partner>().AnyAsync(p => p.Id == id);

    public Task<bool> ExistsByPhoneAsync(string phone) =>
        _context.Set<Partner>().AnyAsync(p => p.Phone == phone);

    public Task<Partner?> GetByPhoneAsync(string phone) =>
        _context.Set<Partner>().FirstOrDefaultAsync(p => p.Phone == phone);
}
