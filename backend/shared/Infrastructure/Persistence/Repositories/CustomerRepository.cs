using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Application.Customers;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly NestlyDbContext _context;

    public CustomerRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Customer entity)
    {
        await _context.Set<Customer>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Customer entity)
    {
        _context.Set<Customer>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<Customer?> GetByIdAsync(Guid id) =>
        _context.Set<Customer>().FirstOrDefaultAsync(c => c.Id == id);

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        // Projects the two columns the caller needs instead of materializing
        // whole Customer aggregates - same shape as BannerRepository's
        // media/category batch lookups.
        return await _context.Set<Customer>()
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name);
    }

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<Customer>().AnyAsync(c => c.Id == id);

    public Task<bool> ExistsByMobileAsync(string mobile) =>
        _context.Set<Customer>().AnyAsync(c => c.Mobile == mobile);

    public Task<bool> ExistsByEmailAsync(string email) =>
        _context.Set<Customer>().AnyAsync(c => c.Email == email);

    public Task<Customer?> GetByMobileAsync(string mobile) =>
        _context.Set<Customer>().FirstOrDefaultAsync(c => c.Mobile == mobile);

    public Task<bool> ExistsByReferralCodeAsync(string referralCode) =>
        _context.Set<Customer>().AnyAsync(c => c.ReferralCode == referralCode);

    public Task<Customer?> GetByReferralCodeAsync(string referralCode) =>
        _context.Set<Customer>().FirstOrDefaultAsync(c => c.ReferralCode == referralCode);

    /// <summary>
    /// Search/filter with pagination (SRS 12.4.1, task 101a). Booking count
    /// is computed as a per-row correlated subquery against Bookings rather
    /// than a join+GroupBy - a customer has one row in the result regardless
    /// of how many bookings they have, and the subquery is backed by the
    /// existing index on Booking.CustomerId, so it stays cheap at the page
    /// sizes an admin list actually renders.
    ///
    /// String filters use ToLower()+Contains rather than Npgsql's ILike so
    /// the same LINQ translates on both the production Postgres provider
    /// and the SQLite provider the test suite runs against (see TestDatabase).
    /// </summary>
    public async Task<CustomerSearchResult> SearchAsync(CustomerSearchFilter filter)
    {
        var query = _context.Set<Customer>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            string term = filter.Name.ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.Mobile))
        {
            string term = filter.Mobile.ToLower();
            query = query.Where(c => c.Mobile.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.Email))
        {
            string term = filter.Email.ToLower();
            query = query.Where(c => c.Email != null && c.Email.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            string term = filter.City.ToLower();
            query = query.Where(c => c.City != null && c.City.ToLower().Contains(term));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(c => c.Status == filter.Status.Value);
        }

        if (filter.RegisteredFromUtc.HasValue)
        {
            query = query.Where(c => c.CreatedAt >= filter.RegisteredFromUtc.Value);
        }

        if (filter.RegisteredToUtc.HasValue)
        {
            query = query.Where(c => c.CreatedAt <= filter.RegisteredToUtc.Value);
        }

        var projected = query
            .Select(c => new
            {
                Customer = c,
                BookingCount = _context.Bookings.Count(b => b.CustomerId == c.Id)
            });

        if (filter.MinBookingCount.HasValue)
        {
            projected = projected.Where(x => x.BookingCount >= filter.MinBookingCount.Value);
        }

        if (filter.MaxBookingCount.HasValue)
        {
            projected = projected.Where(x => x.BookingCount <= filter.MaxBookingCount.Value);
        }

        int totalCount = await projected.CountAsync();

        var page = await projected
            .OrderByDescending(x => x.Customer.CreatedAt)
            .ApplyPaging(filter.Page, filter.PageSize)
            .ToListAsync();

        var rows = page.Select(x => new CustomerSearchRow(x.Customer, x.BookingCount)).ToList();
        return new CustomerSearchResult(rows, totalCount);
    }

    /// <summary>Top N cities by customer count, for the Customer Analytics breakdown table (task: Admin Web Customer Analytics dashboard).</summary>
    private const int TopCitiesLimit = 10;

    /// <inheritdoc/>
    public async Task<CustomerAnalyticsCounts> GetAnalyticsCountsAsync(int trendDays, CancellationToken cancellationToken = default)
    {
        // Local "today" pinned once so every query below (and the caller's
        // echoed window) agrees on the same instant - mirrors
        // ProviderRepository.GetOnboardingOverviewCountsAsync's own
        // single-`date` convention.
        var todayUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var trendStartUtc = todayUtc.AddDays(-(trendDays - 1));
        var sevenDayStartUtc = todayUtc.AddDays(-6);
        var tomorrowUtc = todayUtc.AddDays(1);

        var customers = _context.Set<Customer>().AsNoTracking();

        int totalCustomers = await customers.CountAsync(cancellationToken);

        // One round trip for the four status buckets rather than four
        // separate CountAsync calls.
        var statusCounts = await customers
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        int CountFor(CustomerStatus status) => statusCounts.FirstOrDefault(s => s.Status == status)?.Count ?? 0;

        int newToday = await customers.CountAsync(c => c.CreatedAt >= todayUtc && c.CreatedAt < tomorrowUtc, cancellationToken);
        int newLast7Days = await customers.CountAsync(c => c.CreatedAt >= sevenDayStartUtc, cancellationToken);
        int newInTrendWindow = await customers.CountAsync(c => c.CreatedAt >= trendStartUtc, cancellationToken);

        // Activation half of the acquisition-vs-activation funnel: an EXISTS
        // subquery against Bookings (backed by its CustomerId index), same
        // correlated-subquery shape SearchAsync's own BookingCount uses -
        // never a join+Distinct that would fan out per booking.
        int customersWithBookings = await customers
            .CountAsync(c => _context.Bookings.Any(b => b.CustomerId == c.Id), cancellationToken);

        // Registration trend: one GroupBy over the window, then zero-fill
        // every day so the chart never has to guess at a missing point.
        var trendRaw = await customers
            .Where(c => c.CreatedAt >= trendStartUtc)
            .GroupBy(c => c.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var trendByDate = trendRaw.ToDictionary(x => DateOnly.FromDateTime(x.Date), x => x.Count);

        var registrationTrend = new List<CustomerRegistrationTrendPoint>(trendDays);
        for (var date = DateOnly.FromDateTime(trendStartUtc); date <= DateOnly.FromDateTime(todayUtc); date = date.AddDays(1))
        {
            registrationTrend.Add(new CustomerRegistrationTrendPoint(date, trendByDate.GetValueOrDefault(date)));
        }

        var topCities = await customers
            .Where(c => c.City != null && c.City != "")
            .GroupBy(c => c.City!)
            .Select(g => new { City = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.City)
            .Take(TopCitiesLimit)
            .ToListAsync(cancellationToken);

        return new CustomerAnalyticsCounts(
            TotalCustomers: totalCustomers,
            ActiveCount: CountFor(CustomerStatus.Active),
            BlockedCount: CountFor(CustomerStatus.Blocked),
            UnverifiedCount: CountFor(CustomerStatus.Unverified),
            SoftDeletedCount: CountFor(CustomerStatus.SoftDeleted),
            NewToday: newToday,
            NewLast7Days: newLast7Days,
            NewInTrendWindow: newInTrendWindow,
            CustomersWithBookings: customersWithBookings,
            RegistrationTrend: registrationTrend,
            TopCities: topCities.Select(x => new CustomerCityBreakdown(x.City, x.Count)).ToList());
    }
}
