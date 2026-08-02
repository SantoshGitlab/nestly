using Microsoft.EntityFrameworkCore;
using Nestly.Application.Subscriptions;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class SubscriptionPlanRepository : ISubscriptionPlanRepository
{
    private readonly NestlyDbContext _context;

    public SubscriptionPlanRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<SubscriptionPlan?> GetByIdAsync(Guid id) =>
        _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id);

    public Task<bool> NameExistsAsync(string name) =>
        _context.SubscriptionPlans.AnyAsync(p => p.Name == name.Trim());

    public async Task AddAsync(SubscriptionPlan plan)
    {
        await _context.SubscriptionPlans.AddAsync(plan);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SubscriptionPlan plan)
    {
        _context.SubscriptionPlans.Update(plan);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> ListAllAsync() =>
        await _context.SubscriptionPlans
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync();

    public async Task<IReadOnlyList<SubscriptionPlan>> ListActiveAsync() =>
        await _context.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ToListAsync();
}
