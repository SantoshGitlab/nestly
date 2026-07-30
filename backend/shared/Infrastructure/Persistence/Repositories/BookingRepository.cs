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
        // A same-context TransitionTo() call (Payment/Refund services load,
        // transition, and save within one request-scoped context) appends a
        // brand-new BookingStatusHistory row with a client-generated Guid
        // key. EF's automatic change detection has no "loaded from DB"
        // baseline to compare that key against, and - reached only via
        // fixup from an already-tracked (Modified) parent rather than an
        // explicit Add() - defaults to treating a non-default key as an
        // existing row that needs UPDATE rather than a new one that needs
        // INSERT. That turns the insert into a no-op UPDATE and throws a
        // spurious DbUpdateConcurrencyException ("0 rows affected"). Even
        // checking Entry(x).State before SaveChanges doesn't help - by then
        // EF's own lazy detection has usually already misclassified it. The
        // only reliable fix is to settle "is this row actually in the
        // database yet" against the database itself and force the state
        // explicitly, rather than trust EF's heuristic.
        var persistedHistoryIds = new HashSet<Guid>(
            await _context.BookingStatusHistories.AsNoTracking()
                .Where(h => h.BookingId == booking.Id)
                .Select(h => h.Id)
                .ToListAsync());

        foreach (var history in booking.StatusHistory)
        {
            if (!persistedHistoryIds.Contains(history.Id))
            {
                _context.Entry(history).State = EntityState.Added;
            }
        }

        // Only attach+mark-modified when the booking isn't already tracked
        // by this context - the common case (loaded via this same context)
        // needs no attach at all; ordinary change detection handles its
        // modified scalar properties correctly on its own.
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
