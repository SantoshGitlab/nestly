using FluentValidation;

namespace Nestly.Application.Support;

/// <summary>Bounds paging and the date-range filter, same convention as <c>ReviewModerationSearchRequestValidator</c> (task 120f).</summary>
public class AdminSupportTicketSearchRequestValidator : AbstractValidator<AdminSupportTicketSearchRequest>
{
    public const int MaxPageSize = 100;

    public AdminSupportTicketSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(x => x.ToUtc)
            .GreaterThanOrEqualTo(x => x.FromUtc!.Value)
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue)
            .WithMessage("The date-to filter must be on or after the date-from filter.");
    }
}

public class AssignSupportTicketRequestValidator : AbstractValidator<AssignSupportTicketRequest>
{
    public AssignSupportTicketRequestValidator()
    {
        RuleFor(x => x.AdminUserId).NotEmpty();
    }
}

public class ResolveSupportTicketRequestValidator : AbstractValidator<ResolveSupportTicketRequest>
{
    public ResolveSupportTicketRequestValidator()
    {
        RuleFor(x => x.ResolutionSummary).NotEmpty().MaximumLength(2000);
    }
}

public class LinkSupportTicketBookingRequestValidator : AbstractValidator<LinkSupportTicketBookingRequest>
{
    public LinkSupportTicketBookingRequestValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
    }
}
