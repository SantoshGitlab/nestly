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

    public Task<BookingCancellation?> GetByBookingIdAsync(Guid bookingId) =>
        _context.BookingCancellations.FirstOrDefaultAsync(c => c.BookingId == bookingId);
}
