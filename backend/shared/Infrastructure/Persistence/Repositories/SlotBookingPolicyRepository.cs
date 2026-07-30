using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class SlotBookingPolicyRepository : ISlotBookingPolicyRepository
{
    private readonly NestlyDbContext _context;

    public SlotBookingPolicyRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SlotBookingPolicy entity)
    {
        await _context.Set<SlotBookingPolicy>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SlotBookingPolicy entity)
    {
        _context.Set<SlotBookingPolicy>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<SlotBookingPolicy?> GetByIdAsync(Guid id) =>
        _context.Set<SlotBookingPolicy>().FirstOrDefaultAsync(p => p.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<SlotBookingPolicy>().AnyAsync(p => p.Id == id);

    public Task<SlotBookingPolicy?> GetByCityAsync(Guid cityId) =>
        _context.Set<SlotBookingPolicy>().FirstOrDefaultAsync(p => p.CityId == cityId);
}
