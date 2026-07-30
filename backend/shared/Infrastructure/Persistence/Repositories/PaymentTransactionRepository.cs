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
        // See the identical (and more detailed) comment in
        // BookingRepository.UpdateAsync: a same-context retry's newly-added
        // PaymentAttempt (StartAttempt) has a client-generated Guid key that
        // EF's automatic change detection cannot distinguish from an
        // existing row once it's only reachable via fixup from an
        // already-tracked parent, and misclassifies as Modified instead of
        // Added - turning its INSERT into a spurious no-op UPDATE. Settle
        // "is this row actually in the database yet" against the database
        // itself and force the state explicitly, rather than trust EF's
        // heuristic (which by the time SaveChanges runs has often already
        // guessed wrong, even if checked just beforehand).
        var persistedAttemptIds = new HashSet<Guid>(
            await _context.PaymentAttempts.AsNoTracking()
                .Where(a => a.PaymentTransactionId == transaction.Id)
                .Select(a => a.Id)
                .ToListAsync());

        foreach (var attempt in transaction.Attempts)
        {
            if (!persistedAttemptIds.Contains(attempt.Id))
            {
                _context.Entry(attempt).State = EntityState.Added;
            }
        }

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
