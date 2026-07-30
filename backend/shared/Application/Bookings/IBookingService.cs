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
    /// </summary>
    Task<Result<BookingDetailResponse>> CreateAsync(Guid customerId, BookingSummaryRequest request);

    Task<Result<IReadOnlyList<BookingListItemResponse>>> ListAsync(Guid customerId, BookingStatusBucket? bucket);

    Task<Result<BookingDetailResponse>> GetDetailAsync(Guid customerId, Guid bookingId);
}
