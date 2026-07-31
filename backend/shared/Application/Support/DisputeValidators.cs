using FluentValidation;

namespace Nestly.Application.Support;

public class ResolveDisputeRequestValidator : AbstractValidator<ResolveDisputeRequest>
{
    public ResolveDisputeRequestValidator()
    {
        RuleFor(x => x.ResolutionSummary).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.RefundAmount).GreaterThan(0).When(x => x.RefundAmount is not null);
    }
}
