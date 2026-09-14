using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Application.ProviderManagement;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ProviderRepository : IProviderRepository
{
    private readonly NestlyDbContext _context;

    public ProviderRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Provider entity)
    {
        await _context.Set<Provider>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Provider entity)
    {
        _context.Set<Provider>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<Provider?> GetByIdAsync(Guid id) =>
        _context.Set<Provider>().FirstOrDefaultAsync(p => p.Id == id);

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesByIdsAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _context.Set<Provider>()
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.DisplayName })
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, ProviderOnboardingStatus>> GetOnboardingStatusesByIdsAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, ProviderOnboardingStatus>();
        }

        return await _context.Set<Provider>()
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.OnboardingStatus })
            .ToDictionaryAsync(p => p.Id, p => p.OnboardingStatus);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Provider>> ListPendingPhotoModerationAsync(CancellationToken cancellationToken = default) =>
        await _context.Set<Provider>()
            .AsNoTracking()
            .Where(p => p.PhotoModerationStatus == ProviderPhotoModerationStatus.Pending)
            // UpdatedAt moves on any profile edit, so it is not a submission
            // timestamp - but it is the only ordering this entity offers, and
            // a photo submission always bumps it. Good enough for "work the
            // oldest first"; it is a queue order, not an audit fact.
            .OrderBy(p => p.UpdatedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Provider>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Set<Provider>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<Provider>().AnyAsync(p => p.Id == id);

    public Task<bool> ExistsByPhoneAsync(string phone) =>
        _context.Set<Provider>().AnyAsync(p => p.Phone == phone);

    public Task<Provider?> GetByPhoneAsync(string phone) =>
        _context.Set<Provider>().FirstOrDefaultAsync(p => p.Phone == phone);

    public Task<bool> ExistsByEmailAsync(string email) =>
        _context.Set<Provider>().AnyAsync(p => p.Email == email);

    public Task<bool> ExistsByReferralCodeAsync(string referralCode) =>
        _context.Set<Provider>().AnyAsync(p => p.ReferralCode == referralCode);

    public Task<Provider?> GetByReferralCodeAsync(string referralCode) =>
        _context.Set<Provider>().FirstOrDefaultAsync(p => p.ReferralCode == referralCode);

    /// <summary>
    /// Search/filter with pagination (task 150a). String filters use
    /// ToLower()+Contains rather than Npgsql's ILike so the same LINQ
    /// translates on both the production Postgres provider and the SQLite
    /// provider the test suite runs against - matching CustomerRepository.SearchAsync.
    /// </summary>
    public async Task<ProviderSearchResult> SearchAsync(ProviderSearchFilter filter)
    {
        var query = _context.Set<Provider>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            string term = filter.Name.ToLower();
            query = query.Where(p => p.LegalName.ToLower().Contains(term) || p.DisplayName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.Phone))
        {
            string term = filter.Phone.ToLower();
            query = query.Where(p => p.Phone.ToLower().Contains(term));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(p => p.Status == filter.Status.Value);
        }

        if (filter.OnboardingStatus.HasValue)
        {
            query = query.Where(p => p.OnboardingStatus == filter.OnboardingStatus.Value);
        }

        if (filter.CityId.HasValue)
        {
            query = query.Where(p => _context.Set<ProviderServiceArea>()
                .Any(a => a.ProviderId == p.Id && a.CityId == filter.CityId.Value && a.IsActive));
        }

        // Provider Onboarding Overview dashboard: lets a tile click-through
        // land here filtered to exactly the day's registration cohort.
        if (filter.CreatedFromUtc.HasValue)
        {
            query = query.Where(p => p.CreatedAt >= filter.CreatedFromUtc.Value);
        }

        if (filter.CreatedToUtc.HasValue)
        {
            query = query.Where(p => p.CreatedAt <= filter.CreatedToUtc.Value);
        }

        int totalCount = await query.CountAsync();

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .ApplyPaging(filter.Page, filter.PageSize)
            .ToListAsync();

        return new ProviderSearchResult(rows, totalCount);
    }

    /// <inheritdoc/>
    public async Task<ProviderOnboardingOverviewCounts> GetOnboardingOverviewCountsAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        // DateOnly.ToDateTime(TimeOnly) alone returns Kind=Unspecified, which
        // Npgsql refuses to bind against a timestamptz column
        // ("Cannot write DateTime with Kind=Unspecified... only UTC is
        // supported") - this endpoint always 500'd against real Postgres,
        // invisible in the SQLite-backed unit tests. Same fix already used
        // by DashboardQueryService/ProviderEarningLedgerRepository's own
        // date-range queries: the explicit-Kind overload.
        var startOfDayUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var startOfNextDayUtc = startOfDayUtc.AddDays(1);

        // One round trip: project just the two status columns for the day's
        // cohort, then compute all six counts in memory - see this method's
        // interface doc comment for why that stays cheap at this scale.
        var cohort = await _context.Set<Provider>()
            .AsNoTracking()
            .Where(p => p.CreatedAt >= startOfDayUtc && p.CreatedAt < startOfNextDayUtc)
            .Select(p => new { p.OnboardingStatus, p.Status })
            .ToListAsync(cancellationToken);

        return new ProviderOnboardingOverviewCounts(
            TodayOnboardingCount: cohort.Count,
            DocumentVerificationCount: cohort.Count(p => p.OnboardingStatus == ProviderOnboardingStatus.KycSubmitted),
            VerifiedCount: cohort.Count(p => p.OnboardingStatus == ProviderOnboardingStatus.KycVerified),
            PendingCount: cohort.Count(p => p.Status == ProviderStatus.PendingVerification),
            LiveCount: cohort.Count(p => p.OnboardingStatus == ProviderOnboardingStatus.Completed),
            ActiveCount: cohort.Count(p => p.Status == ProviderStatus.Active));
    }
}
