using Microsoft.EntityFrameworkCore;
using Nestly.Application.Cancellations;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class BookingCancellationRepository : ICancellationRepository
{
    private readonly NestlyDbContext _context;

    public BookingCancellationRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(BookingCancellation cancellation)
    {
        await _context.BookingCancellations.AddAsync(cancellation);
        await _context.SaveChangesAsync();
    }

    /// <summary>NESTLY-002: mirrors PaymentTransactionRepository.TryAddAsync - the unique index on BookingId (BookingCancellationConfiguration) is what actually makes this race-safe, not this catch block by itself.</summary>
    public async Task<bool> TryAddAsync(BookingCancellation cancellation)
    {
        await _context.BookingCancellations.AddAsync(cancellation);

        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            // Lost the race - detach the failed entity so a later
            // SaveChangesAsync on this same request-scoped context doesn't
            // try to re-submit it and throw again. Mirrors
            // SlotCapacityRepository.TryReserveAsync's identical cleanup for
            // the same reason.
            foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }

    public async Task UpdateAsync(BookingCancellation cancellation)
    {
        // Only attach+mark-modified when not already tracked by this
        // context - see the identical comment in BookingRepository.UpdateAsync.
        // The common case (TryAddAsync's row, later updated by AttachRefund
        // within the same request-scoped context) needs no attach at all.
        if (_context.Entry(cancellation).State == EntityState.Detached)
        {
            _context.BookingCancellations.Update(cancellation);
        }

        await _context.SaveChangesAsync();
    }

    public Task<BookingCancellation?> GetByBookingIdAsync(Guid bookingId) =>
        _context.BookingCancellations.FirstOrDefaultAsync(c => c.BookingId == bookingId);
}
