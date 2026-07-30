using Microsoft.EntityFrameworkCore;
using Nestly.Application.Bookings;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly NestlyDbContext _context;

    public BookingRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Booking booking)
    {
        await _context.Bookings.AddAsync(booking);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Booking booking)
    {
        // Only attach+mark-modified when the booking isn't already tracked
        // by this context - the common case (loaded via this same context,
        // e.g. Payment/Refund services that load, transition, and save
        // within one request-scoped context) needs no attach at all;
        // ordinary change detection handles its modified scalar properties
        // correctly on its own. A same-context TransitionTo() call also
        // appends a brand-new BookingStatusHistory row - see
        // NewOwnedChildEntityInterceptor for why that needs its own,
        // centralized correction rather than being handled here.
        if (_context.Entry(booking).State == EntityState.Detached)
        {
            _context.Bookings.Update(booking);
        }

        await _context.SaveChangesAsync();
    }

    public Task<Booking?> GetByIdAsync(Guid id) =>
        FullyLoaded().FirstOrDefaultAsync(b => b.Id == id);

    public async Task<IReadOnlyList<Booking>> ListByCustomerAsync(Guid customerId, IReadOnlyList<BookingStatus> statuses) =>
        await FullyLoaded()
            .Where(b => b.CustomerId == customerId && statuses.Contains(b.Status))
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToListAsync();

    private IQueryable<Booking> FullyLoaded() =>
        _context.Bookings
            .Include(b => b.Items).ThenInclude(i => i.AddOns)
            .Include(b => b.StatusHistory);
}
