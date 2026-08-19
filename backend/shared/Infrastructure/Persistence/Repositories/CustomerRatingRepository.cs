using Microsoft.EntityFrameworkCore;
using Nestly.Application.CustomerRatings;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CustomerRatingRepository : ICustomerRatingRepository
{
    private readonly NestlyDbContext _context;

    public CustomerRatingRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CustomerRating rating)
    {
        await _context.CustomerRatings.AddAsync(rating);
        await _context.SaveChangesAsync();
    }

    public Task<CustomerRating?> GetByBookingIdAsync(Guid bookingId) =>
        _context.CustomerRatings.FirstOrDefaultAsync(r => r.BookingId == bookingId);

    public async Task<CustomerRatingSummary?> GetSummaryForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        // Same "one aggregate query, absent row means no ratings yet" shape as
        // ReviewRepository.GetProviderRatingAsync.
        var aggregate = await _context.CustomerRatings
            .AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .GroupBy(_ => 1)
            .Select(g => new { Average = g.Average(r => (double)r.Rating), Count = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        return aggregate is null
            ? null
            : new CustomerRatingSummary(customerId, Math.Round(aggregate.Average, 1), aggregate.Count);
    }

    public async Task<IReadOnlyList<CustomerRatingRow>> ListRecentForCustomerAsync(Guid customerId, int limit, CancellationToken cancellationToken = default) =>
        await _context.CustomerRatings
            .AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(limit)
            .Join(_context.Providers, r => r.ProviderId, p => p.Id, (r, p) => new CustomerRatingRow(r.Id, r.BookingId, r.Rating, r.Note, p.DisplayName, r.CreatedAtUtc))
            .ToListAsync(cancellationToken);
}
