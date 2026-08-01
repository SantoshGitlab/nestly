using FluentValidation;

namespace Nestly.Application.PartnerIdentity;

public class SubmitPartnerKycDocumentRequestValidator : AbstractValidator<SubmitPartnerKycDocumentRequest>
{
    public SubmitPartnerKycDocumentRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty();

        RuleFor(x => x.DocType).IsInEnum();

        RuleFor(x => x.FileRef)
            .NotEmpty().WithMessage("A file reference is required")
            .MaximumLength(1000);

        RuleFor(x => x.DocNumber).MaximumLength(100);
    }
}
