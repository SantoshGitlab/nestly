using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class BookingPartnerAssignmentRepository : IBookingPartnerAssignmentRepository
{
    private readonly NestlyDbContext _context;

    public BookingPartnerAssignmentRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(BookingPartnerAssignment entity)
    {
        await _context.BookingPartnerAssignments.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(BookingPartnerAssignment entity)
    {
        if (_context.Entry(entity).State == EntityState.Detached)
        {
            _context.BookingPartnerAssignments.Update(entity);
        }

        await _context.SaveChangesAsync();
    }

    public Task<BookingPartnerAssignment?> GetByIdAsync(Guid id) =>
        _context.BookingPartnerAssignments.FirstOrDefaultAsync(a => a.Id == id);

    public Task<BookingPartnerAssignment?> GetActiveByBookingAsync(Guid bookingId) =>
        _context.BookingPartnerAssignments
            .Where(a => a.BookingId == bookingId &&
                (a.Status == BookingPartnerAssignmentStatus.Assigned || a.Status == BookingPartnerAssignmentStatus.Accepted))
            .OrderByDescending(a => a.AssignedAt)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<BookingPartnerAssignment>> ListByBookingAsync(Guid bookingId) =>
        await _context.BookingPartnerAssignments
            .Where(a => a.BookingId == bookingId)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();

    public async Task<IReadOnlyList<BookingPartnerAssignment>> ListByPartnerAsync(Guid partnerId) =>
        await _context.BookingPartnerAssignments
            .Where(a => a.PartnerId == partnerId)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();
}
