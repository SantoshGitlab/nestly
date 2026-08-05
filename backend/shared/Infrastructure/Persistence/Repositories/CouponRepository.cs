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

    public async Task AddAsync(Coupon coupon)
    {
        await _context.Coupons.AddAsync(coupon);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Coupon coupon)
    {
        _context.Coupons.Update(coupon);
        await _context.SaveChangesAsync();
    }

    public Task<bool> CodeExistsAsync(string code) =>
        _context.Coupons.AnyAsync(c => c.Code == code.Trim().ToUpper());

    public async Task<CouponSearchResult> SearchAsync(CouponSearchFilter filter)
    {
        var query = _context.Coupons.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Code))
        {
            string term = filter.Code.Trim().ToUpper();
            query = query.Where(c => c.Code.Contains(term));
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(c => c.IsActive == filter.IsActive.Value);
        }

        if (filter.DiscountType.HasValue)
        {
            query = query.Where(c => c.DiscountType == filter.DiscountType.Value);
        }

        if (filter.CustomerSegment.HasValue)
        {
            query = query.Where(c => c.CustomerSegment == filter.CustomerSegment.Value);
        }

        if (filter.ApplicableCategoryId.HasValue)
        {
            query = query.Where(c => c.ApplicableCategoryId == filter.ApplicableCategoryId.Value);
        }

        if (filter.ValidOnUtc.HasValue)
        {
            query = query.Where(c => c.ValidFromUtc <= filter.ValidOnUtc.Value && c.ValidToUtc >= filter.ValidOnUtc.Value);
        }

        int totalCount = await query.CountAsync();

        var page = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .ApplyPaging(filter.Page, filter.PageSize)
            .ToListAsync();

        // Category names resolved in one batch query rather than per-row -
        // same N+1 avoidance CategoryCityMappingRepository.ListAsync gets for
        // free from its join, done as a separate lookup here since a
        // coupon's applicable category is optional (a plain join would drop
        // uncategorized coupons, or need a LEFT JOIN Postgres/SQLite both
        // translate less predictably than a Contains lookup).
        var categoryIds = page
            .Where(c => c.ApplicableCategoryId.HasValue)
            .Select(c => c.ApplicableCategoryId!.Value)
            .Distinct()
            .ToList();

        var categoryNames = await _context.Set<Category>()
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name);

        var items = page.Select(c => ToAdminResponse(c, categoryNames)).ToList();
        return new CouponSearchResult(items, totalCount);
    }

    /// <summary>Shared with <see cref="Nestly.Infrastructure.Services.CouponManagementService"/> for single-coupon reads that don't need a batch lookup.</summary>
    internal static CouponAdminResponse ToAdminResponse(Coupon coupon, IReadOnlyDictionary<Guid, string> categoryNames) =>
        new(
            coupon.Id,
            coupon.Code,
            coupon.Description,
            coupon.DiscountType,
            coupon.DiscountValue,
            coupon.MaxDiscountAmount,
            coupon.MinOrderAmount,
            coupon.ValidFromUtc,
            coupon.ValidToUtc,
            coupon.IsActive,
            coupon.UsageLimitTotal,
            coupon.UsageLimitPerCustomer,
            coupon.RedemptionCount,
            coupon.ApplicableCategoryId,
            coupon.ApplicableCategoryId.HasValue && categoryNames.TryGetValue(coupon.ApplicableCategoryId.Value, out string? name) ? name : null,
            coupon.CustomerSegment,
            coupon.CreatedAtUtc);
}
