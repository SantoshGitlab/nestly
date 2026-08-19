using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Application.Bookings;

/// <summary>Creates and reads bookings (SRS 13, task 58-61). Every method is scoped to the caller's own customer id.</summary>
public interface IBookingService
{
    /// <summary>
    /// Validates the same preconditions as <see cref="IBookingSummaryService"/>
    /// (task 58a-f), then persists an immutable snapshot of the result (task
    /// 59) and moves it straight to PaymentPending - there is no payment
    /// gateway yet (Phase 4), so "created" and "awaiting payment" are the
    /// same moment for now.
    ///
    /// <para>
    /// Task 331: the one exception is a booking with nothing payable - an AMC
    /// entitlement redemption (<paramref name="amcContractId"/>), a fully
    /// wallet-covered checkout, or a discount that takes the total to zero.
    /// That booking is created straight into
    /// <see cref="BookingStatus.Confirmed"/> and never enters PaymentPending,
    /// which for it is a dead end: <see cref="Nestly.Domain.PaymentTransaction"/>
    /// rejects a non-positive amount, so <c>IPaymentService</c> has no order to
    /// create and nothing could ever move it on. Callers that assume "a new
    /// booking is awaiting payment" must read the returned status instead.
    /// </para>
    /// </summary>
    /// <param name="recurringBookingPlanId">
    /// Task 297: set only by <c>IRecurringBookingSchedulerService</c> when it
    /// materializes one occurrence of a plan, and stamped onto
    /// <see cref="Booking.RecurringBookingPlanId"/> (task 296's FK). Optional
    /// specifically so the generator stays on this one orchestration instead
    /// of gaining its own creation path just to set one column - every other
    /// caller (the customer's own "Book now") passes nothing and is unaffected.
    /// </param>
    /// <param name="amcContractId">
    /// docs/AMC.md: set only by <c>IAmcCustomerService.RedeemVisitAsync</c>
    /// when a customer redeems entitlement against an active
    /// <see cref="Nestly.Domain.CustomerAmcContract"/>. When set, the booking's
    /// final payable is forced to zero and any coupon/subscription discount
    /// the request would otherwise have picked up is ignored - the contract
    /// already fully covers the visit, and stacking a second discount on top
    /// of a prepaid entitlement is not a combination this module supports.
    /// Optional for the identical reason <paramref name="recurringBookingPlanId"/>
    /// is: every other caller (a customer's own "Book now", the recurring
    /// scheduler) passes nothing and is unaffected.
    /// </param>
    Task<Result<BookingDetailResponse>> CreateAsync(
        Guid customerId,
        BookingSummaryRequest request,
        Guid? recurringBookingPlanId = null,
        Guid? amcContractId = null);

    /// <summary>Paged, newest first (task 301-follow-up), same page-1/size-20 defaults the admin booking search already uses, so a long-tenured customer's history no longer loads and renders as one unbounded page.</summary>
    Task<Result<BookingListResponse>> ListAsync(Guid customerId, BookingStatusBucket? bucket, int page = 1, int pageSize = 20);

    Task<Result<BookingDetailResponse>> GetDetailAsync(Guid customerId, Guid bookingId);
}
