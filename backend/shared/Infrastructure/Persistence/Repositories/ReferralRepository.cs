using Microsoft.EntityFrameworkCore;
using Nestly.Application.Referral;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ReferralRepository : IReferralRepository
{
    private readonly NestlyDbContext _context;

    public ReferralRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<Referral?> GetByIdAsync(Guid id) =>
        _context.Referrals.FirstOrDefaultAsync(r => r.Id == id);

    public Task<Referral?> GetByRefereeCustomerIdAsync(Guid refereeCustomerId) =>
        _context.Referrals.FirstOrDefaultAsync(r => r.RefereeCustomerId == refereeCustomerId);

    public async Task<IReadOnlyList<Referral>> ListByReferrerCustomerIdAsync(Guid referrerCustomerId) =>
        await _context.Referrals
            .Where(r => r.ReferrerCustomerId == referrerCustomerId)
            .OrderByDescending(r => r.RegisteredAtUtc)
            .ToListAsync();

    public Task<int> CountRewardedByReferrerAsync(Guid referrerCustomerId) =>
        _context.Referrals.CountAsync(r => r.ReferrerCustomerId == referrerCustomerId && r.Status == ReferralStatus.Rewarded);

    public async Task<IReadOnlyList<Referral>> ListExpiredAsync(DateTime asOfUtc) =>
        await _context.Referrals
            .Where(r => r.Status == ReferralStatus.Registered && r.ExpiresAtUtc <= asOfUtc)
            .ToListAsync();

    public async Task AddAsync(Referral referral)
    {
        await _context.Referrals.AddAsync(referral);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Referral referral)
    {
        _context.Referrals.Update(referral);
        await _context.SaveChangesAsync();
    }
}
