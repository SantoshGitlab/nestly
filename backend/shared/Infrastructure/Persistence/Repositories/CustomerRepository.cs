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
}
