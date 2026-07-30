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
        _context.Bookings.Update(booking);
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
