using Microsoft.EntityFrameworkCore;
using Nestly.Application.Referral;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ReferralMilestoneRepository : IReferralMilestoneRepository
{
    private readonly NestlyDbContext _context;

    public ReferralMilestoneRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ReferralMilestone>> ListActiveOrderedByThresholdAsync() =>
        await _context.ReferralMilestones
            .Where(m => m.IsActive)
            .OrderBy(m => m.ThresholdCount)
            .ToListAsync();

    public async Task<IReadOnlyList<ReferralMilestone>> ListAllOrderedByThresholdAsync() =>
        await _context.ReferralMilestones
            .OrderBy(m => m.ThresholdCount)
            .ToListAsync();

    public Task<ReferralMilestone?> GetByIdAsync(Guid id) =>
        _context.ReferralMilestones.FirstOrDefaultAsync(m => m.Id == id);

    public Task<bool> ExistsByThresholdAsync(int thresholdCount) =>
        _context.ReferralMilestones.AnyAsync(m => m.ThresholdCount == thresholdCount);

    public async Task AddAsync(ReferralMilestone milestone)
    {
        await _context.ReferralMilestones.AddAsync(milestone);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ReferralMilestone milestone)
    {
        _context.ReferralMilestones.Update(milestone);
        await _context.SaveChangesAsync();
    }
}
