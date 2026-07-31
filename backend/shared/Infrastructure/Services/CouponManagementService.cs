using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Application.Coupons;
using Nestly.Application.Serviceability;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Admin coupon management and redemption reporting (SRS 12.12, task 118).
/// Every mutating rule dimension is delegated to <see cref="Coupon"/>'s own
/// constructor/<c>Update</c> - this service is the I/O-dependent half
/// (uniqueness checks, category existence, persistence) the entity has no
/// business doing itself, the same split <c>CouponService</c>'s doc comment
/// describes for the consumer-facing validate/reserve flow.
/// </summary>
public class CouponManagementService : ICouponManagementService
{
    private readonly ICouponRepository _couponRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly NestlyDbContext _context;

    public CouponManagementService(
        ICouponRepository couponRepository,
        ICategoryRepository categoryRepository,
        NestlyDbContext context)
    {
        _couponRepository = couponRepository;
        _categoryRepository = categoryRepository;
        _context = context;
    }

    public async Task<IReadOnlyList<CategoryLookupResponse>> ListApplicableCategoriesAsync()
    {
        // Empty query matches every active category - SearchActiveAsync's
        // Contains("") is always true - same reuse
        // ServiceabilityMappingManagementService.ListCategoriesAsync makes
        // for its own category picker.
        var categories = await _categoryRepository.SearchActiveAsync(string.Empty);
        return categories.Select(c => new CategoryLookupResponse(c.Id, c.Name)).ToList();
    }

    public async Task<CouponAdminSearchResponse> SearchAsync(CouponAdminSearchRequest request)
    {
        var filter = new CouponSearchFilter(
            request.Code,
            request.IsActive,
            request.DiscountType,
            request.CustomerSegment,
            request.ApplicableCategoryId,
            request.ValidOnUtc,
            request.Page,
            request.PageSize);

        var result = await _couponRepository.SearchAsync(filter);
        return new CouponAdminSearchResponse(result.Items, result.TotalCount, request.Page, request.PageSize);
    }

    public async Task<Result<CouponAdminResponse>> GetByIdAsync(Guid id)
    {
        var coupon = await _couponRepository.GetByIdAsync(id);
        if (coupon is null)
        {
            return Error.NotFound("Coupon.NotFound", "The specified coupon does not exist.");
        }

        return await ToResponseAsync(coupon);
    }

    public async Task<Result<CouponAdminResponse>> CreateAsync(CouponCreateRequest request)
    {
        if (await _couponRepository.CodeExistsAsync(request.Code))
        {
            return Error.Conflict("Coupon.CodeAlreadyExists", "A coupon with this code already exists.");
        }

        if (request.ApplicableCategoryId.HasValue)
        {
            var category = await _categoryRepository.GetByIdAsync(request.ApplicableCategoryId.Value);
            if (category is null)
            {
                return Error.NotFound("Coupon.CategoryNotFound", "The specified category does not exist.");
            }
        }

        var coupon = new Coupon(
            Guid.NewGuid(),
            request.Code,
            request.Description,
            request.DiscountType,
            request.DiscountValue,
            request.MaxDiscountAmount,
            request.MinOrderAmount,
            request.ValidFromUtc,
            request.ValidToUtc,
            request.UsageLimitTotal,
            request.UsageLimitPerCustomer,
            request.ApplicableCategoryId,
            request.CustomerSegment);

        await _couponRepository.AddAsync(coupon);
        return await ToResponseAsync(coupon);
    }

    public async Task<Result<CouponAdminResponse>> UpdateAsync(Guid id, CouponUpdateRequest request)
    {
        var coupon = await _couponRepository.GetByIdAsync(id);
        if (coupon is null)
        {
            return Error.NotFound("Coupon.NotFound", "The specified coupon does not exist.");
        }

        if (request.ApplicableCategoryId.HasValue)
        {
            var category = await _categoryRepository.GetByIdAsync(request.ApplicableCategoryId.Value);
            if (category is null)
            {
                return Error.NotFound("Coupon.CategoryNotFound", "The specified category does not exist.");
            }
        }

        coupon.Update(
            request.Description,
            request.DiscountType,
            request.DiscountValue,
            request.MaxDiscountAmount,
            request.MinOrderAmount,
            request.ValidFromUtc,
            request.ValidToUtc,
            request.UsageLimitTotal,
            request.UsageLimitPerCustomer,
            request.ApplicableCategoryId,
            request.CustomerSegment);

        await _couponRepository.UpdateAsync(coupon);
        return await ToResponseAsync(coupon);
    }

    public async Task<Result> ActivateAsync(Guid id)
    {
        var coupon = await _couponRepository.GetByIdAsync(id);
        if (coupon is null)
        {
            return Result.Failure(Error.NotFound("Coupon.NotFound", "The specified coupon does not exist."));
        }

        coupon.Activate();
        await _couponRepository.UpdateAsync(coupon);
        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(Guid id)
    {
        var coupon = await _couponRepository.GetByIdAsync(id);
        if (coupon is null)
        {
            return Result.Failure(Error.NotFound("Coupon.NotFound", "The specified coupon does not exist."));
        }

        coupon.Deactivate();
        await _couponRepository.UpdateAsync(coupon);
        return Result.Success();
    }

    /// <summary>
    /// Redemption reporting (SRS 12.12.2). Reads <see cref="NestlyDbContext"/>
    /// directly across Coupon/CouponRedemption - the same cross-aggregate
    /// read-model reasoning <c>DashboardQueryService</c>'s doc comment gives:
    /// this has no write side of its own, so routing it through the coupon
    /// repository would only add indirection. The aggregation itself runs
    /// client-side after a single materializing query rather than via
    /// SumAsync/GroupBy-in-SQL, matching DashboardQueryService's decimal-sum
    /// note - the redemption counts a coupon campaign realistically produces
    /// are small enough that this costs nothing in practice, and it keeps
    /// the query provider-agnostic (SQLite in tests, Postgres in production).
    /// </summary>
    public async Task<Result<CouponRedemptionReportResponse>> GetRedemptionReportAsync(CouponRedemptionReportRequest request)
    {
        if (request.FromUtc.HasValue && request.ToUtc.HasValue && request.FromUtc > request.ToUtc)
        {
            return Error.Validation("Coupon.InvalidDateRange", "The 'from' date cannot be after the 'to' date.");
        }

        if (request.CouponId.HasValue && await _couponRepository.GetByIdAsync(request.CouponId.Value) is null)
        {
            return Error.NotFound("Coupon.NotFound", "The specified coupon does not exist.");
        }

        var redemptions = _context.CouponRedemptions.AsQueryable();

        if (request.CouponId.HasValue)
        {
            redemptions = redemptions.Where(r => r.CouponId == request.CouponId.Value);
        }

        if (request.FromUtc.HasValue)
        {
            redemptions = redemptions.Where(r => r.RedeemedAtUtc >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            redemptions = redemptions.Where(r => r.RedeemedAtUtc <= request.ToUtc.Value);
        }

        var rows = await (
            from redemption in redemptions
            join coupon in _context.Coupons on redemption.CouponId equals coupon.Id
            select new
            {
                coupon.Id,
                coupon.Code,
                coupon.IsActive,
                redemption.CustomerId,
                redemption.DiscountAmount
            }).ToListAsync();

        var reportRows = rows
            .GroupBy(r => new { r.Id, r.Code, r.IsActive })
            .Select(g => new CouponRedemptionReportRow(
                g.Key.Id,
                g.Key.Code,
                g.Key.IsActive,
                g.Count(),
                g.Sum(x => x.DiscountAmount),
                g.Select(x => x.CustomerId).Distinct().Count()))
            .OrderByDescending(r => r.RedemptionCount)
            .ToList();

        return new CouponRedemptionReportResponse(
            reportRows,
            rows.Count,
            rows.Sum(r => r.DiscountAmount),
            request.FromUtc,
            request.ToUtc);
    }

    private async Task<CouponAdminResponse> ToResponseAsync(Coupon coupon)
    {
        string? categoryName = null;
        if (coupon.ApplicableCategoryId.HasValue)
        {
            var category = await _categoryRepository.GetByIdAsync(coupon.ApplicableCategoryId.Value);
            categoryName = category?.Name;
        }

        var categoryNames = categoryName is null
            ? new Dictionary<Guid, string>()
            : new Dictionary<Guid, string> { [coupon.ApplicableCategoryId!.Value] = categoryName };

        return CouponRepository.ToAdminResponse(coupon, categoryNames);
    }
}
