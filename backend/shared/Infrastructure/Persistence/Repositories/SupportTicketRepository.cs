using Microsoft.EntityFrameworkCore;
using Nestly.Application.Support;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class SupportTicketRepository : ISupportTicketRepository
{
    private readonly NestlyDbContext _context;

    public SupportTicketRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SupportTicket ticket)
    {
        await _context.SupportTickets.AddAsync(ticket);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SupportTicket ticket)
    {
        _context.SupportTickets.Update(ticket);
        await _context.SaveChangesAsync();
    }

    public Task<SupportTicket?> GetByIdAsync(Guid id) =>
        _context.SupportTickets
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<IReadOnlyList<SupportTicket>> ListByCustomerAsync(Guid customerId) =>
        await _context.SupportTickets
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync();

    public async Task<IReadOnlyList<SupportTicket>> ListByBookingAsync(Guid bookingId) =>
        await _context.SupportTickets
            .Where(t => t.BookingId == bookingId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync();
}
