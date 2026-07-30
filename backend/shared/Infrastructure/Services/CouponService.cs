using Nestly.Application.Bookings;
using Nestly.Application.Coupons;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Coupon validation and redemption (SRS 11.10, 14.2, tasks 72a-d, 73). Pure
/// per-coupon rules (validity window, discount + cap calculation) live on
/// <see cref="Coupon"/> itself; everything here is the I/O-dependent half -
/// reading usage counts and booking history from other aggregates - that a
/// domain entity has no business doing.
/// </summary>
public class CouponService : ICouponService
{
    private readonly ICouponRepository _couponRepository;
    private readonly ICouponRedemptionRepository _redemptionRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly TimeProvider _timeProvider;

    public CouponService(
        ICouponRepository couponRepository,
        ICouponRedemptionRepository redemptionRepository,
        IBookingRepository bookingRepository,
        TimeProvider timeProvider)
    {
        _couponRepository = couponRepository;
        _redemptionRepository = redemptionRepository;
        _bookingRepository = bookingRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CouponSummaryResponse>> ValidateAsync(Guid customerId, string code, Guid categoryId, decimal orderAmount)
    {
        var coupon = await _couponRepository.GetByCodeAsync(code);
        if (coupon is null)
        {
            return Error.NotFound("Coupon.NotFound", "This coupon code does not exist.");
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        if (!coupon.IsWithinValidityWindow(nowUtc))
        {
            return Error.Business("Coupon.NotActive", "This coupon is not currently active or has expired.");
        }

        if (coupon.ApplicableCategoryId is not null && coupon.ApplicableCategoryId != categoryId)
        {
            return Error.Business("Coupon.CategoryNotApplicable", "This coupon does not apply to the selected service's category.");
        }

        if (coupon.CustomerSegment != CouponCustomerSegment.All)
        {
            bool isFirstBooking = await IsFirstBookingCustomerAsync(customerId);

            if (coupon.CustomerSegment == CouponCustomerSegment.FirstBookingOnly && !isFirstBooking)
            {
                return Error.Business("Coupon.FirstBookingOnly", "This coupon is only valid on a customer's first booking.");
            }

            if (coupon.CustomerSegment == CouponCustomerSegment.RepeatBookingOnly && isFirstBooking)
            {
                return Error.Business("Coupon.RepeatBookingOnly", "This coupon is only valid for repeat customers.");
            }
        }

        if (coupon.UsageLimitTotal is not null && coupon.RedemptionCount >= coupon.UsageLimitTotal)
        {
            return Error.Conflict("Coupon.UsageLimitReached", "This coupon has reached its overall usage limit.");
        }

        if (coupon.UsageLimitPerCustomer is not null)
        {
            int usedByCustomer = await _redemptionRepository.CountByCouponAndCustomerAsync(coupon.Id, customerId);
            if (usedByCustomer >= coupon.UsageLimitPerCustomer)
            {
                return Error.Business("Coupon.AlreadyUsedByCustomer", "You have already used this coupon the maximum number of times.");
            }
        }

        if (!coupon.TryCalculateDiscount(orderAmount, out decimal discountAmount))
        {
            return Error.Validation("Coupon.MinOrderAmountNotMet", $"This coupon requires a minimum order amount of {coupon.MinOrderAmount}.");
        }

        return Result.Success(new CouponSummaryResponse(coupon.Id, coupon.Code, coupon.Description, discountAmount));
    }

    public async Task<Result> ReserveAsync(Guid couponId)
    {
        bool reserved = await _couponRepository.TryReserveRedemptionAsync(couponId);
        return reserved
            ? Result.Success()
            : Result.Failure(Error.Conflict("Coupon.UsageLimitReached", "This coupon has reached its overall usage limit."));
    }

    public Task CreateRedemptionRecordAsync(Guid couponId, Guid customerId, Guid bookingId, decimal discountAmount) =>
        _redemptionRepository.AddAsync(new CouponRedemption(Guid.NewGuid(), couponId, customerId, bookingId, discountAmount));

    /// <summary>
    /// "First booking" (SRS 11.10.2) is defined as: no prior booking ever
    /// progressed past Initiated (an abandoned, never-paid-for cart doesn't
    /// count as an order). Reuses ListByCustomerAsync rather than adding a
    /// dedicated count query - booking volume per customer is small enough
    /// that loading the (small) list is not a meaningful cost in this phase.
    /// </summary>
    private async Task<bool> IsFirstBookingCustomerAsync(Guid customerId)
    {
        var pastStatuses = Enum.GetValues<BookingStatus>().Where(s => s != BookingStatus.Initiated).ToList();
        var bookings = await _bookingRepository.ListByCustomerAsync(customerId, pastStatuses);
        return bookings.Count == 0;
    }
}
