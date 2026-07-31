using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Reports;

/// <summary>
/// Standard admin reports (SRS 12.18.1, tasks 128a-128c): booking/revenue,
/// refund, coupon usage, customer segmentation and support ticket stats, each
/// with a matching CSV export. One service across every report rather than
/// one per report - every report shares the same "aggregate a date-bounded
/// read model, no write side" shape <c>DashboardQueryService</c> (task 99)
/// already established for exactly this kind of cross-aggregate query.
/// </summary>
public interface IReportingQueryService
{
    Task<Result<BookingRevenueReportResponse>> GetBookingRevenueReportAsync(BookingRevenueReportRequest request);

    Task<Result<byte[]>> ExportBookingRevenueCsvAsync(BookingRevenueReportRequest request);

    Task<Result<RefundReportResponse>> GetRefundReportAsync(RefundReportRequest request);

    Task<Result<byte[]>> ExportRefundCsvAsync(RefundReportRequest request);

    Task<Result<CouponUsageReportResponse>> GetCouponUsageReportAsync(CouponUsageReportRequest request);

    Task<Result<byte[]>> ExportCouponUsageCsvAsync(CouponUsageReportRequest request);

    Task<Result<CustomerSegmentationReportResponse>> GetCustomerSegmentationReportAsync(CustomerSegmentationReportRequest request);

    Task<Result<byte[]>> ExportCustomerSegmentationCsvAsync(CustomerSegmentationReportRequest request);

    Task<Result<SupportTicketReportResponse>> GetSupportTicketReportAsync(SupportTicketReportRequest request);

    Task<Result<byte[]>> ExportSupportTicketCsvAsync(SupportTicketReportRequest request);
}
