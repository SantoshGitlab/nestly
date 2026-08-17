using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nestly.Application.Bookings;
using Nestly.Application.CustomerRatings;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Provider-side rating submission for a completed job - the reverse-
/// direction analogue of <see cref="ReviewService"/>. Eligibility mirrors
/// it exactly: the booking is Completed, it has no existing rating yet (one
/// rating per booking, same invariant as reviews), and, if
/// <see cref="ReviewPolicyOptions.EnforceSubmissionWindow"/> is set, still
/// within the same configured window since completion - reusing
/// <see cref="ReviewPolicyOptions"/> rather than a near-duplicate options
/// class, since "how long after completion is feedback still accepted" is
/// one policy question regardless of which side is submitting it.
/// </summary>
public class CustomerRatingService : ICustomerRatingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly ICustomerRatingRepository _ratingRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ReviewPolicyOptions _policy;

    public CustomerRatingService(
        IBookingRepository bookingRepository, ICustomerRatingRepository ratingRepository, TimeProvider timeProvider, IOptions<ReviewPolicyOptions> policy)
    {
        _bookingRepository = bookingRepository;
        _ratingRepository = ratingRepository;
        _timeProvider = timeProvider;
        _policy = policy.Value;
    }

    public async Task<Result<CustomerRatingEligibilityResponse>> GetEligibilityAsync(Guid providerId, Guid bookingId)
    {
        var booking = await ResolveOwnedBookingAsync(providerId, bookingId);
        if (booking is null)
        {
            return Error.NotFound("CustomerRating.BookingNotFound", "The specified booking does not exist.");
        }

        return Result.Success(await EvaluateEligibilityAsync(booking));
    }

    public async Task<Result<CustomerRatingResponse?>> GetByBookingAsync(Guid providerId, Guid bookingId)
    {
        var booking = await ResolveOwnedBookingAsync(providerId, bookingId);
        if (booking is null)
        {
            return Error.NotFound("CustomerRating.BookingNotFound", "The specified booking does not exist.");
        }

        var rating = await _ratingRepository.GetByBookingIdAsync(bookingId);
        return Result.Success<CustomerRatingResponse?>(rating is null ? null : ToResponse(rating));
    }

    public async Task<Result<CustomerRatingResponse>> SubmitAsync(Guid providerId, Guid bookingId, SubmitCustomerRatingRequest request)
    {
        var booking = await ResolveOwnedBookingAsync(providerId, bookingId);
        if (booking is null)
        {
            return Error.NotFound("CustomerRating.BookingNotFound", "The specified booking does not exist.");
        }

        var eligibility = await EvaluateEligibilityAsync(booking);
        if (!eligibility.IsEligible)
        {
            return Error.Business("CustomerRating.NotEligible", eligibility.IneligibilityReason!);
        }

        var rating = new CustomerRating(Guid.NewGuid(), bookingId, providerId, booking.CustomerId, request.Rating, request.Note);

        try
        {
            await _ratingRepository.AddAsync(rating);
        }
        catch (DbUpdateException)
        {
            // Same concurrent-submission race as ReviewService.SubmitAsync -
            // the unique index on CustomerRating.BookingId is the real
            // arbiter; the loser gets a clean business error, not a 500.
            return Error.Conflict("CustomerRating.AlreadySubmitted", "This job has already been rated.");
        }

        return Result.Success(ToResponse(rating));
    }

    /// <summary>
    /// A Completed booking's <see cref="Booking.AssignedProviderId"/> is
    /// already the settled "who did this job" attribution (see
    /// ReviewService's own use of it for the reverse direction) - so the
    /// ownership check here is a direct equality against it rather than a
    /// second lookup through the BookingProviderAssignment bridge that
    /// ProviderJobService uses for the in-flight accept/start/complete
    /// actions, where the assignment (not yet reflected as "the" provider)
    /// is still the source of truth.
    /// </summary>
    private async Task<Booking?> ResolveOwnedBookingAsync(Guid providerId, Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        return booking is not null && booking.AssignedProviderId == providerId ? booking : null;
    }

    private async Task<CustomerRatingEligibilityResponse> EvaluateEligibilityAsync(Booking booking)
    {
        if (booking.Status != BookingStatus.Completed)
        {
            return new CustomerRatingEligibilityResponse(false, "Only completed jobs can be rated.");
        }

        var existing = await _ratingRepository.GetByBookingIdAsync(booking.Id);
        if (existing is not null)
        {
            return new CustomerRatingEligibilityResponse(false, "This job has already been rated.");
        }

        if (_policy.EnforceSubmissionWindow)
        {
            var completedAt = booking.StatusHistory
                .Where(h => h.ToStatus == BookingStatus.Completed)
                .Select(h => h.ChangedAtUtc)
                .Cast<DateTime?>()
                .LastOrDefault();

            if (completedAt is not null && _timeProvider.GetUtcNow().DateTime - completedAt.Value > TimeSpan.FromDays(_policy.SubmissionWindowDays))
            {
                return new CustomerRatingEligibilityResponse(false, $"The rating window for this job closed {_policy.SubmissionWindowDays} days after completion.");
            }
        }

        return new CustomerRatingEligibilityResponse(true, null);
    }

    private static CustomerRatingResponse ToResponse(CustomerRating rating) => new(
        rating.Id, rating.BookingId, rating.CustomerId, rating.Rating, rating.Note, rating.CreatedAtUtc);
}
