using FluentAssertions;
using Nestly.Application;
using Nestly.Application.Reports;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers tasks 128a-128c: the standard admin reports (SRS 12.18.1) -
/// booking/revenue, refund, coupon usage, customer segmentation and support
/// ticket stats - each with a matching CSV export. Unlike
/// <see cref="DashboardQueryServiceTests"/> (which shares one <see cref="TestDatabase"/>
/// class fixture and isolates via a per-test city filter), this class gives
/// each test its own fresh database: the customer-segmentation and
/// support-ticket reports have no city dimension to isolate on, so a shared
/// database would let one test's rows leak into another's aggregate counts.
/// </summary>
public sealed class ReportingQueryServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    private static ReportingQueryService BuildService(NestlyDbContext context) => new(context);

    private static Customer NewCustomer(string? city = null) =>
        new(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Test Customer", CustomerStatus.Active, city: city);

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
    public async Task Booking_revenue_report_totals_only_successful_payments_for_bookings_in_range()
    {
        string city = "City-" + Guid.NewGuid();
        var customer = NewCustomer();
        var inRangeBooking = NewBooking(customer.Id, city);
        var outOfRangeBooking = NewBooking(customer.Id, city);
        var payment = NewSuccessfulPayment(inRangeBooking.Id, customer.Id, 610m);

        using (var context = _db.CreateContext())
        {
            context.Add(customer);
            context.Add(inRangeBooking);
            context.Add(outOfRangeBooking);
            context.Add(payment);
            context.SaveChanges();

            Age(context, outOfRangeBooking, nameof(Booking.CreatedAtUtc), DateTime.UtcNow.AddDays(-30));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetBookingRevenueReportAsync(
            new BookingRevenueReportRequest(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), city, CategoryId: null));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalBookingsCount.Should().Be(1);
        result.Value.TotalRevenue.Should().Be(610m);
        result.Value.Rows.Should().ContainSingle(r => r.BookingId == inRangeBooking.Id);
    }

    [Fact]
    public async Task Booking_revenue_csv_export_contains_a_header_and_one_row_per_booking()
    {
        string city = "City-" + Guid.NewGuid();
        var customer = NewCustomer();
        var booking = NewBooking(customer.Id, city);

        using (var context = _db.CreateContext())
        {
            context.Add(customer);
            context.Add(booking);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).ExportBookingRevenueCsvAsync(
            new BookingRevenueReportRequest(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), city, CategoryId: null));

        result.IsSuccess.Should().BeTrue();
        string csv = System.Text.Encoding.UTF8.GetString(result.Value);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().StartWith("BookingId,CreatedAtUtc,City,Status,CouponCode,Subtotal,Tax,Total");
        lines.Should().HaveCount(2);
        lines[1].Should().Contain(booking.Id.ToString());
    }

    [Fact]
    public async Task An_inverted_booking_revenue_date_range_is_rejected()
    {
        using var context = _db.CreateContext();
        var result = await BuildService(context).GetBookingRevenueReportAsync(
            new BookingRevenueReportRequest(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1), City: null, CategoryId: null));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reports.InvalidDateRange");
    }

    [Fact]
    public async Task Refund_report_only_totals_refunded_status_amounts()
    {
        string city = "City-" + Guid.NewGuid();
        var customer = NewCustomer();
        var booking = NewBooking(customer.Id, city);
        var payment = NewSuccessfulPayment(booking.Id, customer.Id, 610m);

        var refunded = new RefundTransaction(Guid.NewGuid(), booking.Id, payment.Id, RefundType.Full, RefundMethod.Gateway, 610m, "Customer cancellation");
        refunded.MarkProcessing();
        refunded.MarkRefunded("gateway-ref-" + Guid.NewGuid());

        var failed = new RefundTransaction(Guid.NewGuid(), booking.Id, payment.Id, RefundType.Full, RefundMethod.Gateway, 200m, "Gateway declined");
        failed.MarkProcessing();
        failed.MarkFailed("Gateway declined the refund.");

        using (var context = _db.CreateContext())
        {
            context.Add(customer);
            context.Add(booking);
            context.Add(payment);
            context.Add(refunded);
            context.Add(failed);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetRefundReportAsync(
            new RefundReportRequest(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)));

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().Contain(r => r.RefundId == refunded.Id);
        result.Value.Rows.Should().Contain(r => r.RefundId == failed.Id);
        result.Value.TotalRefundedAmount.Should().Be(610m);
    }

    [Fact]
    public async Task Coupon_usage_report_groups_redemptions_by_coupon()
    {
        var customer1 = NewCustomer();
        var customer2 = NewCustomer();
        var booking1 = NewBooking(customer1.Id, "City-" + Guid.NewGuid());
        var booking2 = NewBooking(customer2.Id, "City-" + Guid.NewGuid());

        var coupon = new Coupon(
            Guid.NewGuid(), "SAVE10-" + Guid.NewGuid().ToString("N")[..6], "10% off", CouponDiscountType.Percentage, 10m,
            maxDiscountAmount: 100m, minOrderAmount: 0m, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(30),
            usageLimitTotal: null, usageLimitPerCustomer: null, applicableCategoryId: null, CouponCustomerSegment.All);

        var redemption1 = new CouponRedemption(Guid.NewGuid(), coupon.Id, customer1.Id, booking1.Id, 50m);
        var redemption2 = new CouponRedemption(Guid.NewGuid(), coupon.Id, customer2.Id, booking2.Id, 60m);

        using (var context = _db.CreateContext())
        {
            context.Add(customer1);
            context.Add(customer2);
            context.Add(booking1);
            context.Add(booking2);
            context.Add(coupon);
            context.Add(redemption1);
            context.Add(redemption2);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetCouponUsageReportAsync(
            new CouponUsageReportRequest(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)));

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Rows.Should().ContainSingle(r => r.CouponId == coupon.Id).Subject;
        row.CouponCode.Should().Be(coupon.Code);
        row.RedemptionCount.Should().Be(2);
        row.TotalDiscountAmount.Should().Be(110m);
    }

    [Fact]
    public async Task Customer_segmentation_report_groups_by_status_and_city()
    {
        string city = "City-" + Guid.NewGuid();
        var active1 = NewCustomer(city);
        var active2 = NewCustomer(city);
        var blocked = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Blocked Customer", CustomerStatus.Blocked, city: city);

        using (var context = _db.CreateContext())
        {
            context.Add(active1);
            context.Add(active2);
            context.Add(blocked);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetCustomerSegmentationReportAsync(
            new CustomerSegmentationReportRequest(RegisteredFromUtc: null, RegisteredToUtc: null));

        result.IsSuccess.Should().BeTrue();
        result.Value.ByStatus.Should().Contain(r => r.Status == CustomerStatus.Active && r.Count >= 2);
        result.Value.ByCity.Should().Contain(r => r.City == city && r.Count == 3);
    }

    [Fact]
    public async Task Support_ticket_report_computes_average_resolution_hours_for_terminal_tickets_only()
    {
        var customer = NewCustomer();
        var booking = NewBooking(customer.Id, "City-" + Guid.NewGuid());

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

            Age(context, resolvedTicket, nameof(SupportTicket.CreatedAtUtc), DateTime.UtcNow.AddHours(-5));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetSupportTicketReportAsync(
            new SupportTicketReportRequest(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalTickets.Should().Be(2);
        result.Value.ResolvedCount.Should().Be(1);
        result.Value.AverageResolutionHours.Should().BeApproximately(5.0, 0.1);
        result.Value.ByCategory.Should().Contain(r => r.Category == SupportTicketCategory.BookingIssue && r.Count == 2);
    }

    [Fact]
    public async Task Support_ticket_report_average_resolution_is_null_when_nothing_resolved()
    {
        var customer = NewCustomer();
        var booking = NewBooking(customer.Id, "City-" + Guid.NewGuid());
        var openTicket = new SupportTicket(Guid.NewGuid(), customer.Id, booking.Id, SupportTicketCategory.TechnicalIssue, "App crash", "The app crashed during checkout.");

        using (var context = _db.CreateContext())
        {
            context.Add(customer);
            context.Add(booking);
            context.Add(openTicket);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetSupportTicketReportAsync(
            new SupportTicketReportRequest(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)));

        result.IsSuccess.Should().BeTrue();
        result.Value.AverageResolutionHours.Should().BeNull();
    }

    public void Dispose() => _db.Dispose();
}
