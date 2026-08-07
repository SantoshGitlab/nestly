using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Tracking;

/// <summary>
/// The read behind consumer-api's <c>GET /bookings/{bookingId}/tracking</c>
/// (task 275) - the one-shot snapshot the live tracking screen loads before
/// the SignalR hub starts pushing updates into it.
///
/// A query service of its own rather than another method on
/// <see cref="Bookings.IBookingService"/>: that type owns booking creation
/// and the general customer reads, and this is a different thing with a
/// different risk profile - a narrow, PII-bounded, continuously-polled
/// projection whose whole point is exposing less than a booking read does.
/// Keeping it separate is what stops "just add the field to the response"
/// quietly widening it later.
///
/// Read-only. Nothing here writes, and nothing here computes an ETA - task
/// 271 owns that on the ingest and en-route paths, where a route lookup is
/// billed and throttled. A customer opening the tracking screen must not be
/// able to drive the maps bill by pulling to refresh.
/// </summary>
public interface IBookingTrackingQueryService
{
    /// <summary>
    /// The tracking snapshot for a booking the caller owns and that is
    /// currently trackable.
    ///
    /// Returns a NotFound error - never Forbidden - for a booking that does
    /// not exist, belongs to someone else, or is outside its tracking window.
    /// See the implementation for why all three are 404s and why only the
    /// first two are indistinguishable from each other.
    /// </summary>
    Task<Result<BookingTrackingResponse>> GetForCustomerAsync(Guid customerId, Guid bookingId);

    /// <summary>
    /// The same snapshot for admin-web's live ops view (task 284) - no
    /// ownership check, since an admin is not scoped to one customer, but
    /// otherwise identical: still a 404 (never an empty 200) for a
    /// nonexistent booking or one outside its tracking window, using the same
    /// two error codes as <see cref="GetForCustomerAsync"/> so the two
    /// surfaces cannot silently disagree about when tracking exists. The
    /// permission check (an admin needs bookings.read) is the controller's
    /// job, same as every other admin-api read - this method does not take a
    /// caller id to check against, because there is nothing here to check it
    /// against.
    /// </summary>
    Task<Result<BookingTrackingResponse>> GetForAdminAsync(Guid bookingId);
}
