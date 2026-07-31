using FluentValidation;

namespace Nestly.Application.Reports;

public class BookingRevenueReportRequestValidator : AbstractValidator<BookingRevenueReportRequest>
{
    public BookingRevenueReportRequestValidator()
    {
        RuleFor(x => x.ToUtc).GreaterThanOrEqualTo(x => x.FromUtc)
            .WithMessage("The 'to' date cannot be before the 'from' date.");
    }
}

public class RefundReportRequestValidator : AbstractValidator<RefundReportRequest>
{
    public RefundReportRequestValidator()
    {
        RuleFor(x => x.ToUtc).GreaterThanOrEqualTo(x => x.FromUtc)
            .WithMessage("The 'to' date cannot be before the 'from' date.");
    }
}

public class CouponUsageReportRequestValidator : AbstractValidator<CouponUsageReportRequest>
{
    public CouponUsageReportRequestValidator()
    {
        RuleFor(x => x.ToUtc).GreaterThanOrEqualTo(x => x.FromUtc)
            .WithMessage("The 'to' date cannot be before the 'from' date.");
    }
}

public class CustomerSegmentationReportRequestValidator : AbstractValidator<CustomerSegmentationReportRequest>
{
    public CustomerSegmentationReportRequestValidator()
    {
        RuleFor(x => x.RegisteredToUtc)
            .GreaterThanOrEqualTo(x => x.RegisteredFromUtc!.Value)
            .When(x => x.RegisteredFromUtc.HasValue && x.RegisteredToUtc.HasValue)
            .WithMessage("The 'registered to' date cannot be before the 'registered from' date.");
    }
}

public class SupportTicketReportRequestValidator : AbstractValidator<SupportTicketReportRequest>
{
    public SupportTicketReportRequestValidator()
    {
        RuleFor(x => x.ToUtc).GreaterThanOrEqualTo(x => x.FromUtc)
            .WithMessage("The 'to' date cannot be before the 'from' date.");
    }
}

/// <summary>Validates the async export request (task 128d) - date-range rule applies even though <see cref="Domain.ExportReportType.CustomerSegmentation"/> ignores the range once generated, so an obviously-wrong request is rejected up front regardless of report kind.</summary>
public class RequestExportJobRequestValidator : AbstractValidator<RequestExportJobRequest>
{
    public RequestExportJobRequestValidator()
    {
        RuleFor(x => x.ReportType).IsInEnum();
        RuleFor(x => x.ToUtc).GreaterThanOrEqualTo(x => x.FromUtc)
            .WithMessage("The 'to' date cannot be before the 'from' date.");
    }
}
