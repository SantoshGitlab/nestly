using Microsoft.EntityFrameworkCore;
using Nestly.Application.Coupons;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CouponRedemptionRepository : ICouponRedemptionRepository
{
    private readonly NestlyDbContext _context;

    public CouponRedemptionRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CouponRedemption redemption)
    {
        await _context.CouponRedemptions.AddAsync(redemption);
        await _context.SaveChangesAsync();
    }

    public Task<int> CountByCouponAndCustomerAsync(Guid couponId, Guid customerId) =>
        _context.CouponRedemptions.CountAsync(r => r.CouponId == couponId && r.CustomerId == customerId);

    public async Task<IReadOnlyList<CouponRedemption>> ListByCustomerAsync(Guid customerId) =>
        await _context.CouponRedemptions
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.RedeemedAtUtc)
            .ToListAsync();
}
