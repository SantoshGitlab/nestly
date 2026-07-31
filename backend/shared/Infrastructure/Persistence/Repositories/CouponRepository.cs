using Microsoft.EntityFrameworkCore;
using Nestly.Application.Coupons;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CouponRepository : ICouponRepository
{
    private readonly NestlyDbContext _context;

    public CouponRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<Coupon?> GetByIdAsync(Guid id) =>
        _context.Coupons.FirstOrDefaultAsync(c => c.Id == id);

    public Task<Coupon?> GetByCodeAsync(string code) =>
        _context.Coupons.FirstOrDefaultAsync(c => c.Code == code.Trim().ToUpper());

    public async Task<bool> TryReserveRedemptionAsync(Guid couponId)
    {
        // A single conditional UPDATE, not read-then-write: the WHERE clause
        // re-checks the cap in the same statement that increments the
        // counter, so two concurrent bookings racing for the last
        // redemption cannot both succeed (task 72c).
        int affected = await _context.Coupons
            .Where(c => c.Id == couponId && (c.UsageLimitTotal == null || c.RedemptionCount < c.UsageLimitTotal))
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.RedemptionCount, c => c.RedemptionCount + 1));

        return affected == 1;
    }
}
