using Microsoft.EntityFrameworkCore;
using Nestly.Application.Refunds;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class RefundTransactionRepository : IRefundTransactionRepository
{
    private readonly NestlyDbContext _context;

    public RefundTransactionRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RefundTransaction refund)
    {
        await _context.RefundTransactions.AddAsync(refund);
        await _context.SaveChangesAsync();
    }

    public Task<RefundTransaction?> GetByIdAsync(Guid id) =>
        _context.RefundTransactions.FirstOrDefaultAsync(r => r.Id == id);

    public async Task<IReadOnlyList<RefundTransaction>> ListByBookingAsync(Guid bookingId) =>
        await _context.RefundTransactions
            .Where(r => r.BookingId == bookingId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

    public async Task<IReadOnlyList<RefundTransaction>> ListByPaymentTransactionAsync(Guid paymentTransactionId) =>
        await _context.RefundTransactions
            .Where(r => r.PaymentTransactionId == paymentTransactionId)
            .ToListAsync();
}
