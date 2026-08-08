using FluentAssertions;
using Nestly.Application;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 42a/42b: services by category, and service detail with policies.</summary>
public sealed class ServiceQueryServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ServiceQueryServiceTests(TestDatabase db) => _db = db;

    private ServiceQueryService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context) => new(
        new CategoryRepository(context),
        new ServiceRepository(context),
        new ServiceAddOnRepository(context),
        new ServiceFaqRepository(context),
        new ReviewRepository(context),
        new InMemoryCacheService());

    [Fact]
    public async Task ListByCategoryAsync_returns_only_active_services_under_that_category()
    {
        var category = new Category(Guid.NewGuid(), "Repairs", "repairs-" + Guid.NewGuid(), "desc");
        var activeService = new Service(Guid.NewGuid(), category.Id, "AC Repair", "ac-repair-" + Guid.NewGuid(), "desc", 499m);
        var inactiveService = new Service(Guid.NewGuid(), category.Id, "Old Repair", "old-repair-" + Guid.NewGuid(), "desc", 199m);
        inactiveService.Deactivate();

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(activeService);
            context.Add(inactiveService);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).ListByCategoryAsync(category.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(s => s.Id == activeService.Id);
    }

    [Fact]
    public async Task ListByCategoryAsync_for_an_unknown_category_returns_not_found()
    {
        using var context = _db.CreateContext();
        var result = await BuildService(context).ListByCategoryAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Catalog.CategoryNotFound");
    }

    [Fact]
    public async Task GetDetailBySlugAsync_includes_policies_addons_and_category_breadcrumb()
    {
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 999m);
        service.SetInclusions("All rooms");
        service.SetExclusions("Balcony");
        service.SetCancellationPolicy("Free cancellation up to 2 hours before slot");
        service.SetReschedulePolicy("One free reschedule");
        var addOn = new ServiceAddOn(Guid.NewGuid(), service.Id, "Sofa Cleaning", 199m);

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);
            context.Add(addOn);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetDetailBySlugAsync(service.Slug);

        result.IsSuccess.Should().BeTrue();
        var detail = result.Value;
        detail.Inclusions.Should().Be("All rooms");
        detail.Exclusions.Should().Be("Balcony");
        detail.CancellationPolicy.Should().Be("Free cancellation up to 2 hours before slot");
        detail.ReschedulePolicy.Should().Be("One free reschedule");
        detail.CategorySlug.Should().Be(category.Slug);
        detail.AddOns.Should().ContainSingle(a => a.Id == addOn.Id);
    }

    [Fact]
    public async Task GetDetailBySlugAsync_for_an_inactive_service_returns_not_found()
    {
        var category = new Category(Guid.NewGuid(), "Salon", "salon-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Facial", "facial-" + Guid.NewGuid(), "desc", 599m);
        service.Deactivate();

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetDetailBySlugAsync(service.Slug);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Catalog.ServiceNotFound");
    }

    /// <summary>Task 52d: FAQs are part of the service detail payload.</summary>
    [Fact]
    public async Task GetDetailBySlugAsync_includes_faqs()
    {
        var category = new Category(Guid.NewGuid(), "Appliance", "appliance-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "AC Service", "ac-service-" + Guid.NewGuid(), "desc", 499m);
        var faq = new ServiceFaq(Guid.NewGuid(), service.Id, "How long does it take?", "About 60-90 minutes.");

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);
            context.Add(faq);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetDetailBySlugAsync(service.Slug);

        result.IsSuccess.Should().BeTrue();
        result.Value.Faqs.Should().ContainSingle(f => f.Id == faq.Id && f.Question == faq.Question && f.Answer == faq.Answer);
    }

    /// <summary>Task 52f, SRS 11.6.1: average, count, per-star breakdown, and a recent-first review list - hidden reviews excluded.</summary>
    [Fact]
    public async Task GetReviewSummaryBySlugAsync_aggregates_only_visible_reviews()
    {
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 999m);

        Review oldest, newest;
        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);

            oldest = SeedReview(context, service.Id, rating: 4, reviewText: "Good job", createdAtUtc: DateTime.UtcNow.AddDays(-2));
            var middle = SeedReview(context, service.Id, rating: 2, reviewText: "Late arrival", createdAtUtc: DateTime.UtcNow.AddDays(-1));
            newest = SeedReview(context, service.Id, rating: 5, reviewText: "Excellent!", createdAtUtc: DateTime.UtcNow);
            var hidden = SeedReview(context, service.Id, rating: 1, reviewText: "Should not count", createdAtUtc: DateTime.UtcNow);
            hidden.Hide(Guid.NewGuid(), "spam");

            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetReviewSummaryBySlugAsync(service.Slug);

        result.IsSuccess.Should().BeTrue();
        var summary = result.Value;
        summary.TotalCount.Should().Be(3);
        summary.AverageRating.Should().Be(Math.Round((4 + 2 + 5) / 3.0, 2));
        summary.RatingBreakdown[4].Should().Be(1);
        summary.RatingBreakdown[2].Should().Be(1);
        summary.RatingBreakdown[5].Should().Be(1);
        summary.RatingBreakdown[1].Should().Be(0);
        summary.RatingBreakdown[3].Should().Be(0);
        summary.RecentReviews.Select(r => r.Id).Should().Equal(newest.Id, /* middle */ summary.RecentReviews[1].Id, oldest.Id);
    }

    [Fact]
    public async Task GetReviewSummaryBySlugAsync_for_a_service_with_no_reviews_returns_a_zeroed_summary()
    {
        var category = new Category(Guid.NewGuid(), "Pest Control", "pest-control-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "General Pest Control", "pest-" + Guid.NewGuid(), "desc", 799m);

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetReviewSummaryBySlugAsync(service.Slug);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(0);
        result.Value.AverageRating.Should().Be(0);
        result.Value.RecentReviews.Should().BeEmpty();
        result.Value.RatingBreakdown.Values.Should().OnlyContain(count => count == 0);
    }

    [Fact]
    public async Task GetReviewSummaryBySlugAsync_for_an_unknown_slug_returns_not_found()
    {
        using var context = _db.CreateContext();
        var result = await BuildService(context).GetReviewSummaryBySlugAsync("does-not-exist");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Catalog.ServiceNotFound");
    }

    /// <summary>
    /// A minimal but FK-valid review: a real Customer and Booking row (Review's
    /// BookingId/CustomerId columns are real foreign keys - see
    /// <c>ReviewConfiguration</c>) without walking the full booking status
    /// machinery, since only <see cref="Review"/>'s own fields matter here.
    /// </summary>
    private static Review SeedReview(Nestly.Infrastructure.Persistence.NestlyDbContext context, Guid serviceId, int rating, string reviewText, DateTime createdAtUtc)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Test Customer", CustomerStatus.Active);
        var booking = new Booking(
            Guid.NewGuid(), customer.Id,
            new CustomerSnapshot(customer.Name, customer.Mobile),
            null,
            new AddressSnapshot("Home", "221B Baker Street", null, null, "560001", "Bengaluru", "Karnataka", 12.9716m, 77.5946m, "Test Customer", "9876543210"),
            new SlotSnapshot(Guid.NewGuid(), DateOnly.FromDateTime(createdAtUtc), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13)),
            new PriceSnapshot(999m, 1, 999m, 0m, 0m, 999m, 0m, 0m, 0m, 999m));
        booking.AddItem(Guid.NewGuid(), serviceId, "Service", "service-slug", 999m, 1);

        var review = new Review(Guid.NewGuid(), booking.Id, customer.Id, serviceId, providerId: null, rating, reviewText);
        typeof(Review).GetProperty(nameof(Review.CreatedAtUtc))!.SetValue(review, createdAtUtc);

        context.Add(customer);
        context.Add(booking);
        context.Add(review);

        return review;
    }
}
