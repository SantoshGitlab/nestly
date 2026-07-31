using FluentValidation;

namespace Nestly.Application.Support;

public class CreateSupportTicketRequestValidator : AbstractValidator<CreateSupportTicketRequest>
{
    public CreateSupportTicketRequestValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
    }
}

public class AddSupportTicketCommentRequestValidator : AbstractValidator<AddSupportTicketCommentRequest>
{
    public AddSupportTicketCommentRequestValidator()
    {
        RuleFor(x => x.Comment).NotEmpty().MaximumLength(2000);
    }
}
