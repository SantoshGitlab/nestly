using Microsoft.EntityFrameworkCore;
using Nestly.Application.Referral;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ReferralMilestoneAwardRepository : IReferralMilestoneAwardRepository
{
    private readonly NestlyDbContext _context;

    public ReferralMilestoneAwardRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<bool> ExistsAsync(Guid referralMilestoneId, Guid referrerCustomerId) =>
        _context.ReferralMilestoneAwards.AnyAsync(a =>
            a.ReferralMilestoneId == referralMilestoneId && a.ReferrerCustomerId == referrerCustomerId);

    public async Task AddAsync(ReferralMilestoneAward award)
    {
        await _context.ReferralMilestoneAwards.AddAsync(award);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ReferralMilestoneAward>> ListInRangeAsync(DateTime? fromUtc, DateTime? toUtc)
    {
        var query = _context.ReferralMilestoneAwards.AsQueryable();
        if (fromUtc.HasValue)
        {
            query = query.Where(a => a.AwardedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(a => a.AwardedAtUtc <= toUtc.Value);
        }

        return await query.ToListAsync();
    }
}
