using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class BookingTrackingRepository : IBookingTrackingRepository
{
    private readonly NestlyDbContext _context;

    public BookingTrackingRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<BookingTracking?> GetByBookingAsync(Guid bookingId) =>
        _context.Set<BookingTracking>()
            .FirstOrDefaultAsync(x => x.BookingId == bookingId);

    public async Task AddAsync(BookingTracking entity)
    {
        await _context.Set<BookingTracking>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(BookingTracking entity)
    {
        _context.Set<BookingTracking>().Update(entity);
        await _context.SaveChangesAsync();
    }
}
