using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

/// <summary>
/// Enforces SlotWindow.MaxBookingsPerSlot (task 135c). The reservation is a
/// single conditional UPDATE against a per-(window, day) counter row, never
/// a read-then-write - the same pattern
/// <see cref="CouponRepository.TryReserveRedemptionAsync"/> uses to enforce a
/// coupon's usage cap, so two customers racing for the last seat on a
/// promoted slot cannot both win.
///
/// The counter row is created lazily on the first booking for a given
/// window+day. If two concurrent requests both race to create that first
/// row, the unique index on (SlotWindowId, SlotDate) - see
/// SlotBookingCounterConfiguration - lets exactly one INSERT win; the loser
/// falls back to the same conditional UPDATE the row would have taken had it
/// already existed, so the outcome is still capacity-correct.
/// </summary>
public class SlotCapacityRepository : ISlotCapacityRepository
{
    private readonly NestlyDbContext _context;

    public SlotCapacityRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<bool> TryReserveAsync(Guid slotWindowId, DateOnly date, int maxCapacity)
    {
        if (await TryIncrementExistingAsync(slotWindowId, date, maxCapacity))
        {
            return true;
        }

        // ExecuteUpdateAsync can't tell "no row yet" apart from "row exists
        // and is full" - both match zero rows. Assume the optimistic case
        // (nothing booked yet today) and try to create the counter.
        try
        {
            _context.Set<SlotBookingCounter>().Add(new SlotBookingCounter(Guid.NewGuid(), slotWindowId, date, 1));
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            // Lost the race to create the row - another request's insert
            // already committed (or the row genuinely existed and was full
            // all along). Detach the failed entity so it doesn't get
            // re-submitted by a later SaveChangesAsync on this same
            // request-scoped context, then re-check against the real row.
            foreach (var entry in _context.ChangeTracker.Entries<SlotBookingCounter>().ToList())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.State = EntityState.Detached;
                }
            }

            return await TryIncrementExistingAsync(slotWindowId, date, maxCapacity);
        }
    }

    public async Task ReleaseAsync(Guid slotWindowId, DateOnly date)
    {
        // Conditional on BookedCount > 0 for the same reason the reservation
        // is conditional on BookedCount < max: a single UPDATE that cannot be
        // lost to a concurrent one, and that can never take the counter
        // negative if a release is somehow issued twice for one booking.
        await _context.Set<SlotBookingCounter>()
            .Where(c => c.SlotWindowId == slotWindowId && c.SlotDate == date && c.BookedCount > 0)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.BookedCount, c => c.BookedCount - 1));
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetBookedCountsAsync(IReadOnlyCollection<Guid> slotWindowIds, DateOnly date)
    {
        if (slotWindowIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _context.Set<SlotBookingCounter>()
            .AsNoTracking()
            .Where(c => c.SlotDate == date && slotWindowIds.Contains(c.SlotWindowId))
            .ToDictionaryAsync(c => c.SlotWindowId, c => c.BookedCount);
    }

    private async Task<bool> TryIncrementExistingAsync(Guid slotWindowId, DateOnly date, int maxCapacity)
    {
        int affected = await _context.Set<SlotBookingCounter>()
            .Where(c => c.SlotWindowId == slotWindowId && c.SlotDate == date && c.BookedCount < maxCapacity)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.BookedCount, c => c.BookedCount + 1));

        return affected == 1;
    }
}
