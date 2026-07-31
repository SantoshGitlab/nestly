using Microsoft.EntityFrameworkCore;
using Nestly.Application.Reviews;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly NestlyDbContext _context;

    public ReviewRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Review review)
    {
        await _context.Reviews.AddAsync(review);
        await _context.SaveChangesAsync();
    }

    public Task<Review?> GetByBookingIdAsync(Guid bookingId) =>
        _context.Reviews.FirstOrDefaultAsync(r => r.BookingId == bookingId);

    public async Task<IReadOnlyList<Review>> ListByServiceAsync(Guid serviceId) =>
        await _context.Reviews
            .Where(r => r.ServiceId == serviceId && r.Status == ReviewStatus.Visible)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();
}
