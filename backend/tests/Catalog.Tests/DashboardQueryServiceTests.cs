using FluentAssertions;
using Nestly.Application;
using Nestly.Application.Dashboard;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 99: the admin dashboard KPI query (SRS 12.3) - bookings,
/// revenue, cancellations, refunds, and open support tickets, each scoped by
/// date range/city/category.
///
/// Every test filters by a City unique to that test (a fresh
/// <c>Guid</c>-suffixed value) even when city filtering itself isn't what's
/// under test - <see cref="TestDatabase"/> is a class fixture shared by every
/// test method here, so without that isolation a count-based assertion in one
/// test could pick up bookings seeded by another.
/// </summary>
public sealed class DashboardQueryServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public DashboardQueryServiceTests(TestDatabase db) => _db = db;

    private static DashboardQueryService BuildService(NestlyDbContext context) => new(context);

    private static Customer NewCustomer() =>
        new(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Test Customer", CustomerStatus.Active);

    private static Booking NewBooking(Guid customerId, string city) => new(
        Guid.NewGuid(),
        customerId,
        new CustomerSnapshot("Test Customer", "9876543210"),
        sourceAddressId: null,
        new AddressSnapshot("Home", "221B Baker Street", null, null, "560001", city, "Karnataka", 12.9716m, 77.5946m, "Test Customer", "9876543210"),
        new SlotSnapshot(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13)),
        new PriceSnapshot(500m, 1, 500m, 0m, 0m, 500m, 18m, 90m, 20m, 610m));

    private static PaymentTransaction NewSuccessfulPayment(Guid bookingId, Guid customerId, decimal amount)
    {
        var payment = new PaymentTransaction(Guid.NewGuid(), bookingId, customerId, amount, "INR", "idem-" + Guid.NewGuid());
        var attempt = payment.StartAttempt(Guid.NewGuid(), "order-" + Guid.NewGuid());
        payment.MarkAttemptSucceeded(attempt.Id, "pay-" + Guid.NewGuid());
        return payment;
    }

    private static void Age(NestlyDbContext context, object entity, string propertyName, DateTime value) =>
        context.Entry(entity).Property(propertyName).CurrentValue = value;

    [Fact]
    public async Task Unfiltered_date_range_defaults_to_today_and_excludes_older_bookings()
    {
        string city = "City-" + Guid.NewGuid();
        var customer = NewCustomer();
        var todayBooking = NewBooking(customer.Id, city);
        var lastWeekBooking = NewBooking(customer.Id, city);

        using (var context = _db.CreateContext())
        {
            context.Add(customer);
            context.Add(todayBooking);
            context.Add(lastWeekBooking);
            context.SaveChanges();

            Age(context, lastWeekBooking, nameof(Booking.CreatedAtUtc), DateTime.UtcNow.AddDays(-7));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetKpisAsync(new DashboardFilterRequest(null, null, city, null));

        result.IsSuccess.Should().BeTrue();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        result.Value.DateFrom.Should().Be(today);
        result.Value.DateTo.Should().Be(today);
        result.Value.BookingsCount.Should().Be(1);
    }

    [Fact]
    public async Task Explicit_date_range_includes_bookings_across_the_whole_window()
    {
        string city = "City-" + Guid.NewGuid();
        var customer = NewCustomer();
        var todayBooking = NewBooking(customer.Id, city);
        var lastWeekBooking = NewBooking(customer.Id, city);

        using (var context = _db.CreateContext())
        {
            context.Add(customer);
            context.Add(todayBooking);
            context.Add(lastWeekBooking);
            context.SaveChanges();

            Age(context, lastWeekBooking, nameof(Booking.CreatedAtUtc), DateTime.UtcNow.AddDays(-7));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var filter = new DashboardFilterRequest(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            DateOnly.FromDateTime(DateTime.UtcNow),
            city,
            null);
        var result = await BuildService(readContext).GetKpisAsync(filter);

        result.IsSuccess.Should().BeTrue();
        result.Value.BookingsCount.Should().Be(2);
    }

    [Fact]
    public async Task Revenue_counts_only_successful_payments()
    {
        string city = "City-" + Guid.NewGuid();
        var customer = NewCustomer();
        var paidBooking = NewBooking(customer.Id, city);
        var unpaidBooking = NewBooking(customer.Id, city);
        var successfulPayment = NewSuccessfulPayment(paidBooking.Id, customer.Id, 610m);

        var failedPayment = new PaymentTransaction(Guid.NewGuid(), unpaidBooking.Id, customer.Id, 610m, "INR", "idem-" + Guid.NewGuid());
        var failedAttempt = failedPayment.StartAttempt(Guid.NewGuid(), "order-" + Guid.NewGuid());
        failedPayment.MarkAttemptFailed(failedAttempt.Id, "insufficient funds");

        using (var context = _db.CreateContext())
        {
            context.Add(customer);
            context.Add(paidBooking);
            context.Add(unpaidBooking);
            context.Add(successfulPayment);
            context.Add(failedPayment);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetKpisAsync(new DashboardFilterRequest(null, null, city, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.BookingsCount.Should().Be(2);
        result.Value.RevenueTotal.Should().Be(610m);
    }

    [Fact]
    public async Task Cancellations_and_refunded_amount_are_counted_for_bookings_in_range()
    {
        string city = "City-" + Guid.NewGuid();
        var customer = NewCustomer();
        var cancelledBooking = NewBooking(customer.Id, city);
        var payment = NewSuccessfulPayment(cancelledBooking.Id, customer.Id, 610m);

        var cancellation = new BookingCancellation(
            Guid.NewGuid(), cancelledBooking.Id, CancellationActor.Customer, "Change of plans",
            withinFreeCancellationWindow: true, cancellationFeeAmount: 0m, refundAmount: 610m);

        var refund = new RefundTransaction(
            Guid.NewGuid(), cancelledBooking.Id, payment.Id, RefundType.Full, RefundMethod.Gateway, 610m, "Customer cancellation");
        refund.MarkProcessing();
        refund.MarkRefunded("gateway-ref-" + Guid.NewGuid());

        using (var context = _db.CreateContext())
        {
            context.Add(customer);
            context.Add(cancelledBooking);
            context.Add(payment);
            context.Add(cancellation);
            context.Add(refund);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetKpisAsync(new DashboardFilterRequest(null, null, city, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.CancellationsCount.Should().Be(1);
        result.Value.RefundAmountTotal.Should().Be(610m);
    }

    [Fact]
    public async Task Category_filter_matches_only_bookings_whose_item_belongs_to_that_category()
    {
        string city = "City-" + Guid.NewGuid();
        var customer = NewCustomer();

        var targetCategory = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var targetService = new Service(Guid.NewGuid(), targetCategory.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 500m);
        var otherCategory = new Category(Guid.NewGuid(), "Painting", "painting-" + Guid.NewGuid(), "desc");
        var otherService = new Service(Guid.NewGuid(), otherCategory.Id, "Wall Paint", "wall-paint-" + Guid.NewGuid(), "desc", 700m);

        var matchingBooking = NewBooking(customer.Id, city);
        matchingBooking.AddItem(Guid.NewGuid(), targetService.Id, targetService.Name, targetService.Slug, targetService.Price, 1);
        var otherBooking = NewBooking(customer.Id, city);
        otherBooking.AddItem(Guid.NewGuid(), otherService.Id, otherService.Name, otherService.Slug, otherService.Price, 1);

        using (var context = _db.CreateContext())
        {
            context.Add(customer);
            context.Add(targetCategory);
            context.Add(targetService);
            context.Add(otherCategory);
            context.Add(otherService);
            context.Add(matchingBooking);
            context.Add(otherBooking);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetKpisAsync(new DashboardFilterRequest(null, null, city, targetCategory.Slug));

        result.IsSuccess.Should().BeTrue();
        result.Value.BookingsCount.Should().Be(1);
    }

    [Fact]
    public async Task Open_support_tickets_exclude_resolved_and_closed_tickets()
    {
        string city = "City-" + Guid.NewGuid();
        var customer = NewCustomer();
        var booking = NewBooking(customer.Id, city);

        var openTicket = new SupportTicket(Guid.NewGuid(), customer.Id, booking.Id, SupportTicketCategory.BookingIssue, "Late arrival", "The professional arrived late.");
        var resolvedTicket = new SupportTicket(Guid.NewGuid(), customer.Id, booking.Id, SupportTicketCategory.BookingIssue, "Wrong item", "Wrong service performed.");
        resolvedTicket.ChangeStatus(SupportTicketStatus.InProgress);
        resolvedTicket.ChangeStatus(SupportTicketStatus.Resolved, "Refunded and re-serviced.");

        using (var context = _db.CreateContext())
        {
            context.Add(customer);
            context.Add(booking);
            context.Add(openTicket);
            context.Add(resolvedTicket);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetKpisAsync(new DashboardFilterRequest(null, null, city, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.OpenSupportTicketsCount.Should().Be(1);
    }

    [Fact]
    public async Task An_inverted_date_range_is_rejected_as_a_validation_error()
    {
        using var context = _db.CreateContext();
        var filter = new DashboardFilterRequest(
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            null,
            null);

        var result = await BuildService(context).GetKpisAsync(filter);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Dashboard.InvalidDateRange");
    }
}
