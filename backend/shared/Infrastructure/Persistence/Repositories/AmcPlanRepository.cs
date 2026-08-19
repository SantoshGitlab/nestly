using Microsoft.EntityFrameworkCore;
using Nestly.Application.Amc;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class AmcPlanRepository : IAmcPlanRepository
{
    private readonly NestlyDbContext _context;

    public AmcPlanRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<AmcPlan?> GetByIdAsync(Guid id) =>
        _context.AmcPlans.FirstOrDefaultAsync(p => p.Id == id);

    public Task<bool> NameExistsAsync(string name) =>
        _context.AmcPlans.AnyAsync(p => p.Name == name);

    public async Task AddAsync(AmcPlan plan)
    {
        await _context.AmcPlans.AddAsync(plan);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(AmcPlan plan)
    {
        _context.AmcPlans.Update(plan);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<AmcPlan>> ListAllAsync() =>
        await _context.AmcPlans.OrderByDescending(p => p.CreatedAtUtc).ToListAsync();

    public async Task<IReadOnlyList<AmcPlan>> ListActiveAsync() =>
        await _context.AmcPlans.Where(p => p.IsActive).OrderBy(p => p.Price).ToListAsync();
}
