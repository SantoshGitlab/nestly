using Nestly.Domain;

namespace Nestly.Application.Reports;

/// <summary>
/// Booking and revenue report (SRS 12.18.1 "Booking report"/"Revenue
/// report", task 128a). <see cref="City"/>/<see cref="CategoryId"/> reuse the
/// same optional dimensions <c>BookingSearchFilter</c> already supports.
/// </summary>
public sealed record BookingRevenueReportRequest(DateTime FromUtc, DateTime ToUtc, string? City, Guid? CategoryId);

public sealed record BookingRevenueReportRow(
    Guid BookingId,
    DateTime CreatedAtUtc,
    string City,
    BookingStatus Status,
    string? CouponCode,
    decimal SubtotalSnapshot,
    decimal TaxAmountSnapshot,
    decimal TotalPayableSnapshot);

/// <summary><see cref="TotalRevenue"/> is the sum of successful payment transactions for bookings in range - not the sum of booking totals, which would double-count a booking whose payment failed and was retried, or ignore that a later refund reduced it (same revenue definition <c>DashboardQueryService</c> already uses).</summary>
public sealed record BookingRevenueReportResponse(
    IReadOnlyList<BookingRevenueReportRow> Rows, int TotalBookingsCount, decimal TotalRevenue);

/// <summary>Refund report (SRS 12.18.1 "Refund report", task 128b).</summary>
public sealed record RefundReportRequest(DateTime FromUtc, DateTime ToUtc);

public sealed record RefundReportRow(
    Guid RefundId,
    Guid BookingId,
    RefundType Type,
    RefundMethod Method,
    decimal Amount,
    RefundStatus Status,
    string Reason,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);

public sealed record RefundReportResponse(IReadOnlyList<RefundReportRow> Rows, int TotalCount, decimal TotalRefundedAmount);

/// <summary>Coupon usage report (SRS 12.18.1 "Coupon usage report", task 128b) - one row per coupon with redemptions in range, not one row per redemption, since "usage" is inherently a per-coupon aggregate.</summary>
public sealed record CouponUsageReportRequest(DateTime FromUtc, DateTime ToUtc);

public sealed record CouponUsageReportRow(Guid CouponId, string CouponCode, int RedemptionCount, decimal TotalDiscountAmount);

public sealed record CouponUsageReportResponse(
    IReadOnlyList<CouponUsageReportRow> Rows, int TotalRedemptions, decimal TotalDiscountAmount);

/// <summary>Customer segmentation report (SRS 12.18.1 "Customer report", task 128c) - current customer counts by status and by city, optionally scoped to a registration window.</summary>
public sealed record CustomerSegmentationReportRequest(DateTime? RegisteredFromUtc, DateTime? RegisteredToUtc);

public sealed record CustomerStatusSegmentRow(CustomerStatus Status, int Count);

public sealed record CustomerCitySegmentRow(string City, int Count);

public sealed record CustomerSegmentationReportResponse(
    int TotalCustomers,
    IReadOnlyList<CustomerStatusSegmentRow> ByStatus,
    IReadOnlyList<CustomerCitySegmentRow> ByCity);

/// <summary>Support ticket report (SRS 12.18.1 "Support ticket report", task 128c).</summary>
public sealed record SupportTicketReportRequest(DateTime FromUtc, DateTime ToUtc);

public sealed record SupportTicketCategoryVolumeRow(SupportTicketCategory Category, int Count);

/// <summary>
/// <see cref="AverageResolutionHours"/> is derived from resolved/closed
/// tickets' <c>CreatedAtUtc</c> to <c>UpdatedAtUtc</c> - <see cref="SupportTicket"/>
/// has no dedicated "resolved at" timestamp distinct from its general
/// last-modified stamp, so this is an approximation (the last status change
/// on a resolved/closed ticket is almost always the resolution itself); null
/// when no ticket in range reached a terminal status.
/// </summary>
public sealed record SupportTicketReportResponse(
    int TotalTickets,
    int ResolvedCount,
    double? AverageResolutionHours,
    IReadOnlyList<SupportTicketCategoryVolumeRow> ByCategory);
