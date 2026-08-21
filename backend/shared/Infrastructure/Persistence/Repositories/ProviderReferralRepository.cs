using Microsoft.EntityFrameworkCore;
using Nestly.Application.ProviderReferral;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ProviderReferralRepository : IProviderReferralRepository
{
    private readonly NestlyDbContext _context;

    public ProviderReferralRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<ProviderReferral?> GetByIdAsync(Guid id) =>
        _context.ProviderReferrals.FirstOrDefaultAsync(r => r.Id == id);

    public Task<ProviderReferral?> GetByRefereeProviderIdAsync(Guid refereeProviderId) =>
        _context.ProviderReferrals.FirstOrDefaultAsync(r => r.RefereeProviderId == refereeProviderId);

    public Task<ProviderReferral?> GetByQualifyingBookingIdAsync(Guid bookingId) =>
        _context.ProviderReferrals.FirstOrDefaultAsync(r => r.QualifyingBookingId == bookingId);

    public async Task<IReadOnlyList<ProviderReferral>> ListByReferrerProviderIdAsync(Guid referrerProviderId) =>
        await _context.ProviderReferrals
            .Where(r => r.ReferrerProviderId == referrerProviderId)
            .OrderByDescending(r => r.RegisteredAtUtc)
            .ToListAsync();

    public Task<int> CountRewardedByReferrerAsync(Guid referrerProviderId) =>
        _context.ProviderReferrals.CountAsync(
            r => r.ReferrerProviderId == referrerProviderId && r.Status == ProviderReferralStatus.Rewarded);

    public async Task<IReadOnlyList<ProviderReferral>> ListExpiredAsync(DateTime asOfUtc) =>
        await _context.ProviderReferrals
            .Where(r => r.Status == ProviderReferralStatus.Registered && r.ExpiresAtUtc <= asOfUtc)
            .ToListAsync();

    public async Task<(IReadOnlyList<ProviderReferral> Items, int TotalCount)> SearchAsync(
        ProviderReferralStatus? status, bool? isFraudFlagged, IReadOnlyList<Guid>? providerIds, int page, int pageSize)
    {
        var query = _context.ProviderReferrals.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (isFraudFlagged.HasValue)
        {
            query = query.Where(r => r.IsFraudFlagged == isFraudFlagged.Value);
        }

        if (providerIds is { Count: > 0 })
        {
            query = query.Where(r => providerIds.Contains(r.ReferrerProviderId) || providerIds.Contains(r.RefereeProviderId));
        }

        int totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.RegisteredAtUtc)
            .ApplyPaging(page, pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task AddAsync(ProviderReferral referral)
    {
        await _context.ProviderReferrals.AddAsync(referral);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ProviderReferral referral)
    {
        _context.ProviderReferrals.Update(referral);
        await _context.SaveChangesAsync();
    }
}
