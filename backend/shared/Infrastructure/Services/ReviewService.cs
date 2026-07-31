using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nestly.Application.Bookings;
using Nestly.Application.Reviews;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Review submission eligibility and confirmation (SRS 11.16, 17, tasks
/// 85a-c). Eligibility is: the booking is Completed (SRS 11.16.1), it has no
/// existing review yet (SRS 11.16.3 - one primary review per booking), and,
/// if <see cref="ReviewPolicyOptions.EnforceSubmissionWindow"/> is set,
/// still within the configured window since completion (SRS 11.16.3
/// "may be time-limited").
/// </summary>
public class ReviewService : IReviewService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IReviewRepository _reviewRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ReviewPolicyOptions _policy;

    public ReviewService(
        IBookingRepository bookingRepository, IReviewRepository reviewRepository, TimeProvider timeProvider, IOptions<ReviewPolicyOptions> policy)
    {
        _bookingRepository = bookingRepository;
        _reviewRepository = reviewRepository;
        _timeProvider = timeProvider;
        _policy = policy.Value;
    }

    public async Task<Result<ReviewEligibilityResponse>> GetEligibilityAsync(Guid customerId, Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null || booking.CustomerId != customerId)
        {
            return Error.NotFound("Review.BookingNotFound", "The specified booking does not exist.");
        }

        return Result.Success(await EvaluateEligibilityAsync(booking));
    }

    public async Task<Result<ReviewResponse?>> GetByBookingAsync(Guid customerId, Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null || booking.CustomerId != customerId)
        {
            return Error.NotFound("Review.BookingNotFound", "The specified booking does not exist.");
        }

        var review = await _reviewRepository.GetByBookingIdAsync(bookingId);
        return Result.Success<ReviewResponse?>(review is null ? null : ToResponse(review));
    }

    public async Task<Result<ReviewResponse>> SubmitAsync(Guid customerId, Guid bookingId, SubmitReviewRequest request)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null || booking.CustomerId != customerId)
        {
            return Error.NotFound("Review.BookingNotFound", "The specified booking does not exist.");
        }

        var eligibility = await EvaluateEligibilityAsync(booking);
        if (!eligibility.IsEligible)
        {
            return Error.Business("Review.NotEligible", eligibility.IneligibilityReason!);
        }

        Guid serviceId = booking.Items.Count > 0
            ? booking.Items[0].ServiceId
            : throw new InvalidOperationException($"Booking {bookingId} has no items to resolve a service from.");

        var review = new Review(Guid.NewGuid(), bookingId, customerId, serviceId, request.Rating, request.ReviewText, request.IssueTags);

        try
        {
            await _reviewRepository.AddAsync(review);
        }
        catch (DbUpdateException)
        {
            // Two concurrent submissions for the same booking both pass the
            // eligibility check above and race to insert - the unique index
            // on Review.BookingId (ReviewConfiguration) is the real
            // arbiter; the loser gets a clean business error instead of an
            // unhandled 500.
            return Error.Conflict("Review.AlreadySubmitted", "This booking has already been reviewed.");
        }

        return Result.Success(ToResponse(review));
    }

    private async Task<ReviewEligibilityResponse> EvaluateEligibilityAsync(Booking booking)
    {
        if (booking.Status != BookingStatus.Completed)
        {
            return new ReviewEligibilityResponse(false, "Only completed bookings can be reviewed.");
        }

        var existing = await _reviewRepository.GetByBookingIdAsync(booking.Id);
        if (existing is not null)
        {
            return new ReviewEligibilityResponse(false, "This booking has already been reviewed.");
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
                return new ReviewEligibilityResponse(false, $"The review window for this booking closed {_policy.SubmissionWindowDays} days after completion.");
            }
        }

        return new ReviewEligibilityResponse(true, null);
    }

    private static ReviewResponse ToResponse(Review review) => new(
        review.Id, review.BookingId, review.ServiceId, review.Rating, review.ReviewText, review.IssueTags, review.Status, review.CreatedAtUtc);
}
