using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Application.Reports;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Csv;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Standard admin reports (SRS 12.18.1, tasks 128a-128c). Reads
/// <see cref="NestlyDbContext"/> directly rather than through per-aggregate
/// repositories - the same reasoning <c>DashboardQueryService</c> (task 99)
/// documents: these are read-only cross-aggregate queries with no write side,
/// so routing every one through a repository would only add indirection.
/// Decimal sums are computed client-side after materializing the matching
/// rows, not via <c>SumAsync</c>, for the same reason <c>DashboardQueryService</c>
/// does: SQLite (this project's unit-test provider) has no server-side
/// translation for a decimal aggregate.
/// </summary>
public sealed class ReportingQueryService : IReportingQueryService
{
    private static readonly SupportTicketStatus[] TerminalTicketStatuses =
        [SupportTicketStatus.Resolved, SupportTicketStatus.Closed];

    private readonly NestlyDbContext _context;

    public ReportingQueryService(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BookingRevenueReportResponse>> GetBookingRevenueReportAsync(BookingRevenueReportRequest request)
    {
        if (request.ToUtc < request.FromUtc)
        {
            return Error.Validation("Reports.InvalidDateRange", "The 'to' date cannot be before the 'from' date.");
        }

        IQueryable<Booking> bookingsInRange = _context.Bookings
            .Where(b => b.CreatedAtUtc >= request.FromUtc && b.CreatedAtUtc <= request.ToUtc);

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            string city = request.City.Trim().ToLower();
            bookingsInRange = bookingsInRange.Where(b => b.AddressCitySnapshot.ToLower() == city);
        }

        if (request.CategoryId.HasValue)
        {
            Guid categoryId = request.CategoryId.Value;
            IQueryable<Guid> serviceIdsInCategory = _context.Set<Service>()
                .Where(s => s.CategoryId == categoryId)
                .Select(s => s.Id);

            bookingsInRange = bookingsInRange.Where(b => b.Items.Any(item => serviceIdsInCategory.Contains(item.ServiceId)));
        }

        var rows = await bookingsInRange
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new BookingRevenueReportRow(
                b.Id, b.CreatedAtUtc, b.AddressCitySnapshot, b.Status, b.CouponCodeSnapshot,
                b.SubtotalSnapshot, b.TaxAmountSnapshot, b.TotalPayableSnapshot))
            .ToListAsync();

        IQueryable<Guid> bookingIdsInRange = bookingsInRange.Select(b => b.Id);
        List<decimal> successfulPaymentAmounts = await _context.PaymentTransactions
            .Where(p => p.Status == PaymentTransactionStatus.Success && bookingIdsInRange.Contains(p.BookingId))
            .Select(p => p.Amount)
            .ToListAsync();

        return new BookingRevenueReportResponse(rows, rows.Count, successfulPaymentAmounts.Sum());
    }

    public async Task<Result<byte[]>> ExportBookingRevenueCsvAsync(BookingRevenueReportRequest request)
    {
        var result = await GetBookingRevenueReportAsync(request);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var header = new[] { "BookingId", "CreatedAtUtc", "City", "Status", "CouponCode", "Subtotal", "Tax", "Total" };
        var rows = result.Value.Rows.Select(r => (IReadOnlyList<string?>)new[]
        {
            r.BookingId.ToString(),
            r.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            r.City,
            r.Status.ToString(),
            r.CouponCode,
            r.SubtotalSnapshot.ToString(CultureInfo.InvariantCulture),
            r.TaxAmountSnapshot.ToString(CultureInfo.InvariantCulture),
            r.TotalPayableSnapshot.ToString(CultureInfo.InvariantCulture),
        });

        return CsvWriter.Write(header, rows);
    }

    public async Task<Result<RefundReportResponse>> GetRefundReportAsync(RefundReportRequest request)
    {
        if (request.ToUtc < request.FromUtc)
        {
            return Error.Validation("Reports.InvalidDateRange", "The 'to' date cannot be before the 'from' date.");
        }

        var refunds = await _context.RefundTransactions
            .Where(r => r.CreatedAtUtc >= request.FromUtc && r.CreatedAtUtc <= request.ToUtc)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        var rows = refunds
            .Select(r => new RefundReportRow(
                r.Id, r.BookingId, r.Type, r.Method, r.Amount, r.Status, r.Reason, r.CreatedAtUtc, r.ProcessedAtUtc))
            .ToList();

        decimal totalRefunded = refunds.Where(r => r.Status == RefundStatus.Refunded).Sum(r => r.Amount);

        return new RefundReportResponse(rows, rows.Count, totalRefunded);
    }

    public async Task<Result<byte[]>> ExportRefundCsvAsync(RefundReportRequest request)
    {
        var result = await GetRefundReportAsync(request);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var header = new[] { "RefundId", "BookingId", "Type", "Method", "Amount", "Status", "Reason", "CreatedAtUtc", "ProcessedAtUtc" };
        var rows = result.Value.Rows.Select(r => (IReadOnlyList<string?>)new[]
        {
            r.RefundId.ToString(),
            r.BookingId.ToString(),
            r.Type.ToString(),
            r.Method.ToString(),
            r.Amount.ToString(CultureInfo.InvariantCulture),
            r.Status.ToString(),
            r.Reason,
            r.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            r.ProcessedAtUtc?.ToString("O", CultureInfo.InvariantCulture),
        });

        return CsvWriter.Write(header, rows);
    }

    public async Task<Result<CouponUsageReportResponse>> GetCouponUsageReportAsync(CouponUsageReportRequest request)
    {
        if (request.ToUtc < request.FromUtc)
        {
            return Error.Validation("Reports.InvalidDateRange", "The 'to' date cannot be before the 'from' date.");
        }

        var redemptions = await _context.Set<CouponRedemption>()
            .Where(r => r.RedeemedAtUtc >= request.FromUtc && r.RedeemedAtUtc <= request.ToUtc)
            .ToListAsync();

        var couponIds = redemptions.Select(r => r.CouponId).Distinct().ToList();
        var couponCodesById = couponIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _context.Set<Coupon>()
                .Where(c => couponIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Code);

        var rows = redemptions
            .GroupBy(r => r.CouponId)
            .Select(g => new CouponUsageReportRow(
                g.Key,
                couponCodesById.TryGetValue(g.Key, out var code) ? code : "(deleted coupon)",
                g.Count(),
                g.Sum(r => r.DiscountAmount)))
            .OrderByDescending(r => r.RedemptionCount)
            .ToList();

        return new CouponUsageReportResponse(rows, redemptions.Count, redemptions.Sum(r => r.DiscountAmount));
    }

    public async Task<Result<byte[]>> ExportCouponUsageCsvAsync(CouponUsageReportRequest request)
    {
        var result = await GetCouponUsageReportAsync(request);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var header = new[] { "CouponId", "CouponCode", "RedemptionCount", "TotalDiscountAmount" };
        var rows = result.Value.Rows.Select(r => (IReadOnlyList<string?>)new[]
        {
            r.CouponId.ToString(),
            r.CouponCode,
            r.RedemptionCount.ToString(CultureInfo.InvariantCulture),
            r.TotalDiscountAmount.ToString(CultureInfo.InvariantCulture),
        });

        return CsvWriter.Write(header, rows);
    }

    public async Task<Result<CustomerSegmentationReportResponse>> GetCustomerSegmentationReportAsync(CustomerSegmentationReportRequest request)
    {
        if (request.RegisteredFromUtc.HasValue && request.RegisteredToUtc.HasValue
            && request.RegisteredToUtc.Value < request.RegisteredFromUtc.Value)
        {
            return Error.Validation("Reports.InvalidDateRange", "The 'registered to' date cannot be before the 'registered from' date.");
        }

        IQueryable<Customer> customers = _context.Set<Customer>();

        if (request.RegisteredFromUtc.HasValue)
        {
            customers = customers.Where(c => c.CreatedAt >= request.RegisteredFromUtc.Value);
        }

        if (request.RegisteredToUtc.HasValue)
        {
            customers = customers.Where(c => c.CreatedAt <= request.RegisteredToUtc.Value);
        }

        var loaded = await customers.Select(c => new { c.Status, c.City }).ToListAsync();

        var byStatus = loaded
            .GroupBy(c => c.Status)
            .Select(g => new CustomerStatusSegmentRow(g.Key, g.Count()))
            .OrderBy(r => r.Status)
            .ToList();

        var byCity = loaded
            .Where(c => !string.IsNullOrWhiteSpace(c.City))
            .GroupBy(c => c.City!)
            .Select(g => new CustomerCitySegmentRow(g.Key, g.Count()))
            .OrderByDescending(r => r.Count)
            .ToList();

        return new CustomerSegmentationReportResponse(loaded.Count, byStatus, byCity);
    }

    public async Task<Result<byte[]>> ExportCustomerSegmentationCsvAsync(CustomerSegmentationReportRequest request)
    {
        var result = await GetCustomerSegmentationReportAsync(request);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var header = new[] { "Dimension", "Value", "Count" };
        var rows = result.Value.ByStatus
            .Select(r => (IReadOnlyList<string?>)new[] { "Status", r.Status.ToString(), r.Count.ToString(CultureInfo.InvariantCulture) })
            .Concat(result.Value.ByCity.Select(r =>
                (IReadOnlyList<string?>)new[] { "City", r.City, r.Count.ToString(CultureInfo.InvariantCulture) }));

        return CsvWriter.Write(header, rows);
    }

    public async Task<Result<SupportTicketReportResponse>> GetSupportTicketReportAsync(SupportTicketReportRequest request)
    {
        if (request.ToUtc < request.FromUtc)
        {
            return Error.Validation("Reports.InvalidDateRange", "The 'to' date cannot be before the 'from' date.");
        }

        var tickets = await _context.SupportTickets
            .Where(t => t.CreatedAtUtc >= request.FromUtc && t.CreatedAtUtc <= request.ToUtc)
            .Select(t => new { t.Category, t.Status, t.CreatedAtUtc, t.UpdatedAtUtc })
            .ToListAsync();

        var byCategory = tickets
            .GroupBy(t => t.Category)
            .Select(g => new SupportTicketCategoryVolumeRow(g.Key, g.Count()))
            .OrderByDescending(r => r.Count)
            .ToList();

        var resolved = tickets.Where(t => TerminalTicketStatuses.Contains(t.Status)).ToList();
        double? averageResolutionHours = resolved.Count == 0
            ? null
            : resolved.Average(t => (t.UpdatedAtUtc - t.CreatedAtUtc).TotalHours);

        return new SupportTicketReportResponse(tickets.Count, resolved.Count, averageResolutionHours, byCategory);
    }

    public async Task<Result<byte[]>> ExportSupportTicketCsvAsync(SupportTicketReportRequest request)
    {
        var result = await GetSupportTicketReportAsync(request);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var header = new[] { "Category", "Count" };
        var rows = result.Value.ByCategory.Select(r => (IReadOnlyList<string?>)new[]
        {
            r.Category.ToString(),
            r.Count.ToString(CultureInfo.InvariantCulture),
        });

        return CsvWriter.Write(header, rows);
    }
}
