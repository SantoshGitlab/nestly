using FluentValidation;

namespace Nestly.Application.Customers;

/// <summary>Bounds paging so a caller cannot request an unbounded or negative page (task 101a).</summary>
public class CustomerSearchRequestValidator : AbstractValidator<CustomerSearchRequest>
{
    public const int MaxPageSize = 100;

    public CustomerSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(x => x.RegisteredToUtc)
            .GreaterThanOrEqualTo(x => x.RegisteredFromUtc!.Value)
            .When(x => x.RegisteredFromUtc.HasValue && x.RegisteredToUtc.HasValue)
            .WithMessage("Registered-to date must be on or after the registered-from date.");
        RuleFor(x => x.MaxBookingCount)
            .GreaterThanOrEqualTo(x => x.MinBookingCount!.Value)
            .When(x => x.MinBookingCount.HasValue && x.MaxBookingCount.HasValue)
            .WithMessage("Maximum booking count must be at or above the minimum.");
    }
}

/// <summary>A block reason is required (SRS 12.4.3 admin action) so the audit trail always explains why an account was blocked.</summary>
public class BlockCustomerRequestValidator : AbstractValidator<BlockCustomerRequest>
{
    public BlockCustomerRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public class AddCustomerNoteRequestValidator : AbstractValidator<AddCustomerNoteRequest>
{
    public AddCustomerNoteRequestValidator()
    {
        RuleFor(x => x.Note).NotEmpty().MaximumLength(4000);
    }
}
