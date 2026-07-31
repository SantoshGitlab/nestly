using Microsoft.EntityFrameworkCore;
using Nestly.Application.Payments;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PaymentTransactionRepository : IPaymentTransactionRepository
{
    private readonly NestlyDbContext _context;

    public PaymentTransactionRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PaymentTransaction transaction)
    {
        await _context.PaymentTransactions.AddAsync(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(PaymentTransaction transaction)
    {
        // Only attach+mark-modified when not already tracked by this
        // context - see the identical comment in BookingRepository.UpdateAsync.
        // A same-context retry's newly-added PaymentAttempt is corrected by
        // NewOwnedChildEntityInterceptor, not here.
        if (_context.Entry(transaction).State == EntityState.Detached)
        {
            _context.PaymentTransactions.Update(transaction);
        }

        await _context.SaveChangesAsync();
    }

    public Task<PaymentTransaction?> GetByIdAsync(Guid id) =>
        FullyLoaded().FirstOrDefaultAsync(t => t.Id == id);

    public Task<PaymentTransaction?> GetByBookingIdAsync(Guid bookingId) =>
        FullyLoaded().FirstOrDefaultAsync(t => t.BookingId == bookingId);

    public Task<PaymentTransaction?> GetByGatewayOrderIdAsync(string gatewayOrderId) =>
        FullyLoaded().FirstOrDefaultAsync(t => t.Attempts.Any(a => a.GatewayOrderId == gatewayOrderId));

    public async Task<IReadOnlyList<PaymentTransaction>> ListAsync(DateTime? fromUtc, DateTime? toUtc, PaymentTransactionStatus? status)
    {
        var query = FullyLoaded();

        if (fromUtc is not null)
        {
            query = query.Where(t => t.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc is not null)
        {
            query = query.Where(t => t.CreatedAtUtc <= toUtc.Value);
        }

        if (status is not null)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        return await query.OrderByDescending(t => t.CreatedAtUtc).ToListAsync();
    }

    private IQueryable<PaymentTransaction> FullyLoaded() =>
        _context.PaymentTransactions.Include(t => t.Attempts);
}
