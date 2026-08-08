using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.ProviderManagement;
using Nestly.Application.ProviderProfile;
using Nestly.Application.Tracking;
using Nestly.Domain;
using Nestly.Infrastructure.Migrations;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 293: the two things <c>BookingProviderSummary</c> promised and could
/// not deliver - a provider photo and a provider rating.
///
/// The tests are grouped by the four rules that can each independently make
/// this feature wrong on a customer's screen: the moderation gate (an
/// unreviewed photo must not be visible), the upload validation (the photo
/// reference goes into an <c>img src</c>), the per-provider aggregate (the
/// number must be about this person and must not exist at all when there is
/// nothing to average), and the backfill's reassignment rule (a review must
/// never be attributed to a professional who did not do the job).
/// </summary>
public sealed class ProviderPhotoAndRatingTests : IClassFixture<TestDatabase>
{
    private const string PhotoUrl = "https://cdn.example.com/providers/ravi.jpg";

    private readonly TestDatabase _db;

    public ProviderPhotoAndRatingTests(TestDatabase db) => _db = db;

    // ---- (a) the moderation gate ----

    [Fact]
    public void A_submitted_photo_is_not_visible_to_customers_until_it_is_approved()
    {
        var provider = NewProvider();

        provider.SubmitPhoto(PhotoUrl);

        provider.PhotoUrl.Should().Be(PhotoUrl, "the provider must see their own pending photo");
        provider.PhotoModerationStatus.Should().Be(ProviderPhotoModerationStatus.Pending);
        provider.PublicPhotoUrl.Should().BeNull("a photo nobody has reviewed must not reach a customer");
    }

    [Fact]
    public void An_approved_photo_becomes_visible_to_customers()
    {
        var provider = NewProvider();
        provider.SubmitPhoto(PhotoUrl);

        provider.ApprovePhoto(Guid.NewGuid());

        provider.PublicPhotoUrl.Should().Be(PhotoUrl);
    }

    [Fact]
    public void A_rejected_photo_stays_invisible_but_is_kept_for_the_provider_to_see()
    {
        var provider = NewProvider();
        provider.SubmitPhoto(PhotoUrl);

        provider.RejectPhoto(Guid.NewGuid(), "That is not a photo of a person.");

        provider.PublicPhotoUrl.Should().BeNull();
        provider.PhotoUrl.Should().Be(PhotoUrl, "a rejection the provider cannot see is not actionable");
        provider.PhotoModerationNote.Should().Be("That is not a photo of a person.");
    }

    /// <summary>
    /// The gap a naive implementation leaves open: approve a harmless photo,
    /// then swap the image and keep the approval. Replacing a photo must send
    /// it back to Pending.
    /// </summary>
    [Fact]
    public void Replacing_an_approved_photo_sends_it_back_for_review()
    {
        var provider = NewProvider();
        provider.SubmitPhoto(PhotoUrl);
        provider.ApprovePhoto(Guid.NewGuid());

        provider.SubmitPhoto("https://cdn.example.com/providers/something-else.jpg");

        provider.PhotoModerationStatus.Should().Be(ProviderPhotoModerationStatus.Pending);
        provider.PublicPhotoUrl.Should().BeNull();
        provider.PhotoModeratedByAdminUserId.Should().BeNull("the old verdict described a different image");
    }

    [Fact]
    public async Task An_already_reviewed_photo_cannot_be_ruled_on_twice()
    {
        using var context = _db.CreateContext();
        var provider = NewProvider();
        provider.SubmitPhoto(PhotoUrl);
        context.Add(provider);
        await context.SaveChangesAsync();

        var service = new ProviderPhotoModerationService(new ProviderRepository(context));
        var firstVerdict = await service.ApproveAsync(provider.Id, Guid.NewGuid());
        var secondVerdict = await service.RejectAsync(provider.Id, Guid.NewGuid(), new RejectProviderPhotoRequest("changed my mind"));

        firstVerdict.IsSuccess.Should().BeTrue();
        secondVerdict.IsFailure.Should().BeTrue("a second moderator must not silently overwrite the first one's decision");
        secondVerdict.Error.Code.Should().Be("ProviderPhotoModeration.AlreadyReviewed");
    }

    [Fact]
    public async Task The_moderation_queue_lists_only_photos_awaiting_a_verdict()
    {
        using var context = _db.CreateContext();
        var pending = NewProvider();
        pending.SubmitPhoto(PhotoUrl);
        var approved = NewProvider();
        approved.SubmitPhoto(PhotoUrl);
        approved.ApprovePhoto(Guid.NewGuid());
        var noPhoto = NewProvider();
        context.AddRange(pending, approved, noPhoto);
        await context.SaveChangesAsync();

        var queue = await new ProviderPhotoModerationService(new ProviderRepository(context)).ListPendingAsync();

        queue.Select(row => row.ProviderId).Should().Contain(pending.Id);
        queue.Select(row => row.ProviderId).Should().NotContain([approved.Id, noPhoto.Id]);
    }

    // ---- (a) the photo upload validation ----

    /// <summary>
    /// This value is rendered into an <c>img src</c> on a customer's screen,
    /// so a non-http scheme is script execution rather than a picture.
    /// </summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    [InlineData("file:///etc/passwd")]
    [InlineData("/relative/path.jpg")]
    [InlineData("not a url at all")]
    public void A_photo_reference_that_is_not_an_absolute_http_url_is_rejected(string photoUrl)
    {
        var provider = NewProvider();

        var submit = () => provider.SubmitPhoto(photoUrl);

        submit.Should().Throw<ArgumentException>();
        new UpdateProviderPhotoRequestValidator()
            .Validate(new UpdateProviderPhotoRequest(photoUrl)).IsValid
            .Should().BeFalse("the same rule must produce a 400 rather than an unhandled exception");
    }

    [Theory]
    [InlineData("https://cdn.example.com/a.jpg")]
    [InlineData("http://cdn.example.com/a.jpg")]
    public void A_photo_reference_that_is_an_absolute_http_url_is_accepted(string photoUrl)
    {
        var provider = NewProvider();

        provider.SubmitPhoto(photoUrl);

        provider.PhotoUrl.Should().Be(photoUrl);
        new UpdateProviderPhotoRequestValidator()
            .Validate(new UpdateProviderPhotoRequest(photoUrl)).IsValid.Should().BeTrue();
    }

    // ---- (b) the per-provider rating aggregate ----

    [Fact]
    public async Task A_provider_with_no_reviews_has_no_rating_rather_than_a_rating_of_zero()
    {
        using var context = _db.CreateContext();
        var seed = await SeedAsync(context);

        var rating = await new ReviewRepository(context).GetProviderRatingAsync(seed.Provider.Id);

        rating.Should().BeNull("a brand-new professional must read as unrated, never as badly rated");
    }

    [Fact]
    public async Task The_rating_averages_only_the_reviews_written_about_that_provider()
    {
        using var context = _db.CreateContext();
        var seed = await SeedAsync(context);
        var otherProvider = NewProvider();
        context.Add(otherProvider);
        await context.SaveChangesAsync();

        AddReview(context, seed, seed.Provider.Id, rating: 5);
        AddReview(context, seed, seed.Provider.Id, rating: 4);
        AddReview(context, seed, otherProvider.Id, rating: 1);
        AddReview(context, seed, providerId: null, rating: 1);
        await context.SaveChangesAsync();

        var rating = await new ReviewRepository(context).GetProviderRatingAsync(seed.Provider.Id);

        rating.Should().NotBeNull();
        rating!.AverageRating.Should().Be(4.5);
        rating.ReviewCount.Should().Be(2, "another provider's reviews and unattributed ones belong to nobody's average");
    }

    /// <summary>Hiding a review must remove it from the rating too, or moderation is cosmetic.</summary>
    [Fact]
    public async Task A_hidden_review_does_not_count_towards_the_rating()
    {
        using var context = _db.CreateContext();
        var seed = await SeedAsync(context);

        AddReview(context, seed, seed.Provider.Id, rating: 5);
        var hidden = AddReview(context, seed, seed.Provider.Id, rating: 1);
        hidden.Hide(Guid.NewGuid(), "abusive");
        await context.SaveChangesAsync();

        var rating = await new ReviewRepository(context).GetProviderRatingAsync(seed.Provider.Id);

        rating!.AverageRating.Should().Be(5);
        rating.ReviewCount.Should().Be(1);
    }

    // ---- (b) the summaries the frontends actually render ----

    [Fact]
    public void The_booking_provider_summary_carries_the_real_photo_and_rating()
    {
        var provider = NewProvider();
        provider.SubmitPhoto(PhotoUrl);
        provider.ApprovePhoto(Guid.NewGuid());

        var summary = BookingProviderSummary.From(
            provider, new Application.Reviews.ProviderRatingSummary(provider.Id, 4.8, 25));

        summary.PhotoUrl.Should().Be(PhotoUrl);
        summary.Rating.Should().Be(4.8);
    }

    [Fact]
    public void The_tracking_summary_shows_the_same_photo_and_rating_as_the_booking_detail()
    {
        var provider = NewProvider();
        provider.SubmitPhoto(PhotoUrl);
        provider.ApprovePhoto(Guid.NewGuid());
        var rating = new Application.Reviews.ProviderRatingSummary(provider.Id, 4.8, 25);

        var tracked = TrackedProviderSummary.From(provider, rating);
        var detail = BookingProviderSummary.From(provider, rating);

        tracked.PhotoUrl.Should().Be(detail.PhotoUrl);
        tracked.Rating.Should().Be(detail.Rating);
    }

    /// <summary>
    /// The gate has to hold on the customer-facing surface specifically -
    /// that is the one place a pending photo becoming visible is a real
    /// incident rather than a cosmetic bug.
    /// </summary>
    [Fact]
    public void A_pending_photo_never_reaches_the_booking_or_tracking_summary()
    {
        var provider = NewProvider();
        provider.SubmitPhoto(PhotoUrl);

        BookingProviderSummary.From(provider, null).PhotoUrl.Should().BeNull();
        TrackedProviderSummary.From(provider, null).PhotoUrl.Should().BeNull();
    }

    // ---- helpers ----

    private static Provider NewProvider()
    {
        var provider = new Provider(
            Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual,
            "+9198" + Guid.NewGuid().ToString("N")[..8]);
        provider.ChangeStatus(ProviderStatus.Active);
        return provider;
    }

    internal sealed record Seed(Customer Booker, Service Service, Provider Provider, Booking Booking);

    /// <summary>A customer, a service, a provider and one completed booking joining them - the minimum a review needs to exist against real foreign keys.</summary>
    private static async Task<Seed> SeedAsync(NestlyDbContext context)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Priya Nair", CustomerStatus.Active);
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 500m);
        var provider = NewProvider();
        var booking = NewBooking(customer);

        context.Add(customer);
        context.Add(category);
        context.Add(service);
        context.Add(provider);
        context.Add(booking);
        await context.SaveChangesAsync();

        return new Seed(customer, service, provider, booking);
    }

    private static Booking NewBooking(Customer customer) => new(
        Guid.NewGuid(), customer.Id,
        new CustomerSnapshot(customer.Name, customer.Mobile),
        null,
        new AddressSnapshot("Home", "12 MG Road", null, null, "560001", "Bengaluru", "Karnataka", 12.97m, 77.59m, customer.Name, "9876543210"),
        new SlotSnapshot(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13)),
        new PriceSnapshot(500m, 1, 500m, 0, 0, 500m, 0, 0, 0, 500m));

    /// <summary>
    /// A review against a fresh booking each time - Review.BookingId is
    /// uniquely indexed (one primary review per booking), so several reviews
    /// for one provider necessarily come from several bookings.
    /// </summary>
    private static Review AddReview(NestlyDbContext context, Seed seed, Guid? providerId, int rating)
    {
        var booking = NewBooking(seed.Booker);
        context.Add(booking);

        var review = new Review(Guid.NewGuid(), booking.Id, seed.Booker.Id, seed.Service.Id, providerId, rating, null);
        context.Add(review);
        return review;
    }
}

/// <summary>
/// Task 293: the reassignment rule in the backfill that gives every historic
/// review a provider.
///
/// This runs the migration's own <c>BackfillSql</c> string - not a
/// reimplementation of it - against a seeded database, because the rule it
/// encodes is the one place this feature can do real harm: attributing a
/// one-star review to a professional who never did the job. The three cases
/// below are the three populations that exist in the live table.
/// </summary>
public sealed class ProviderScopedReviewBackfillTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ProviderScopedReviewBackfillTests(TestDatabase db) => _db = db;

    [Fact]
    public async Task A_review_on_a_booking_that_was_never_reassigned_is_attributed_to_its_provider()
    {
        using var context = _db.CreateContext();
        var scenario = await SeedAsync(context, reassigned: false);

        await RunBackfillAsync(context);

        (await ReloadProviderIdAsync(scenario.ReviewId)).Should().Be(scenario.CurrentProviderId);
    }

    /// <summary>
    /// The failure mode this whole rule exists to prevent. The booking now
    /// names the second provider, but nothing proves the second provider is
    /// who the customer was rating - so the review is left unattributed
    /// rather than blamed on them.
    /// </summary>
    [Fact]
    public async Task A_review_on_a_reassigned_booking_is_left_unattributed()
    {
        using var context = _db.CreateContext();
        var scenario = await SeedAsync(context, reassigned: true);

        await RunBackfillAsync(context);

        (await ReloadProviderIdAsync(scenario.ReviewId)).Should().BeNull(
            "attributing a review to a professional who may not have done the job is worse than showing no rating");
    }

    [Fact]
    public async Task A_review_on_a_booking_with_no_provider_at_all_is_left_unattributed()
    {
        using var context = _db.CreateContext();
        var scenario = await SeedAsync(context, reassigned: false, assignProvider: false);

        await RunBackfillAsync(context);

        (await ReloadProviderIdAsync(scenario.ReviewId)).Should().BeNull();
    }

    /// <summary>Re-running the deploy, or a retried migration, must not reattribute a review somebody has since corrected by hand.</summary>
    [Fact]
    public async Task Rerunning_the_backfill_leaves_an_already_attributed_review_alone()
    {
        using var context = _db.CreateContext();
        var scenario = await SeedAsync(context, reassigned: false);
        var correctedTo = Guid.NewGuid();

        await RunBackfillAsync(context);
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO provider (id, legal_name, display_name, provider_type, phone, status, onboarding_status, created_at, updated_at) " +
            "VALUES ({0}, 'Corrected', 'Corrected', 'Individual', {1}, 'Active', 'Completed', {2}, {2});",
            correctedTo, "+9198" + Guid.NewGuid().ToString("N")[..8], DateTime.UtcNow);
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE review SET provider_id = {0} WHERE id = {1};", correctedTo, scenario.ReviewId);

        await RunBackfillAsync(context);

        (await ReloadProviderIdAsync(scenario.ReviewId)).Should().Be(correctedTo);
    }

    private Task RunBackfillAsync(NestlyDbContext context) =>
        context.Database.ExecuteSqlRawAsync(AddProviderPhotoAndProviderScopedReviews.BackfillSql);

    private async Task<Guid?> ReloadProviderIdAsync(Guid reviewId)
    {
        using var context = _db.CreateContext();
        var review = await context.Reviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
        return review.ProviderId;
    }

    private sealed record Scenario(Guid ReviewId, Guid? CurrentProviderId);

    /// <summary>
    /// A completed booking with a review whose <c>provider_id</c> is still
    /// null - i.e. exactly the pre-migration shape - plus the assignment
    /// history that decides whether the backfill may attribute it.
    /// </summary>
    private static async Task<Scenario> SeedAsync(NestlyDbContext context, bool reassigned, bool assignProvider = true)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Priya Nair", CustomerStatus.Active);
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 500m);
        var firstProvider = NewProvider();
        var secondProvider = NewProvider();

        var booking = new Booking(
            Guid.NewGuid(), customer.Id,
            new CustomerSnapshot(customer.Name, customer.Mobile),
            null,
            new AddressSnapshot("Home", "12 MG Road", null, null, "560001", "Bengaluru", "Karnataka", 12.97m, 77.59m, customer.Name, "9876543210"),
            new SlotSnapshot(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13)),
            new PriceSnapshot(500m, 1, 500m, 0, 0, 500m, 0, 0, 0, 500m));

        var currentProviderId = assignProvider
            ? reassigned ? secondProvider.Id : firstProvider.Id
            : (Guid?)null;
        booking.AssignProvider(currentProviderId);

        context.Add(customer);
        context.Add(category);
        context.Add(service);
        context.Add(firstProvider);
        context.Add(secondProvider);
        context.Add(booking);

        if (assignProvider)
        {
            // The history the rule reads. Reassigned bookings keep the
            // superseded row - that surviving row is precisely what makes the
            // attribution unprovable.
            var firstAssignment = new BookingProviderAssignment(
                Guid.NewGuid(), booking.Id, firstProvider.Id, BookingAssignedByType.System, null, null);

            if (reassigned)
            {
                firstAssignment.MarkReassigned();
                context.Add(firstAssignment);
                var secondAssignment = new BookingProviderAssignment(
                    Guid.NewGuid(), booking.Id, secondProvider.Id, BookingAssignedByType.System, null, null);
                secondAssignment.Accept();
                context.Add(secondAssignment);
            }
            else
            {
                firstAssignment.Accept();
                context.Add(firstAssignment);
            }
        }

        var review = new Review(Guid.NewGuid(), booking.Id, customer.Id, service.Id, providerId: null, 1, "Late and rude");
        context.Add(review);
        await context.SaveChangesAsync();

        return new Scenario(review.Id, currentProviderId);
    }

    private static Provider NewProvider()
    {
        var provider = new Provider(
            Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual,
            "+9198" + Guid.NewGuid().ToString("N")[..8]);
        provider.ChangeStatus(ProviderStatus.Active);
        return provider;
    }
}
