using FluentValidation;

namespace Nestly.Application.Bookings;

public class SubmitCompletionProofRequestValidator : AbstractValidator<SubmitCompletionProofRequest>
{
    public SubmitCompletionProofRequestValidator()
    {
        RuleFor(x => x.PhotoRefs)
            .NotNull()
            .Must(refs => refs.Count > 0 && refs.All(r => !string.IsNullOrWhiteSpace(r)))
            .WithMessage("At least one non-empty photo reference is required.");

        RuleForEach(x => x.ChecklistAnswers).ChildRules(answer =>
        {
            answer.RuleFor(a => a.Item).NotEmpty().MaximumLength(500);
            answer.RuleFor(a => a.Notes).MaximumLength(1000);
        });
    }
}
