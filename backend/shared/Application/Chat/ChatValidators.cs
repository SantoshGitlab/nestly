using FluentValidation;

namespace Nestly.Application.Chat;

public class GetOrCreateChatThreadRequestValidator : AbstractValidator<GetOrCreateChatThreadRequest>
{
    public GetOrCreateChatThreadRequestValidator()
    {
        RuleFor(x => x.ContextType).IsInEnum();
        RuleFor(x => x.ContextId).NotEmpty();
    }
}

public class SendChatMessageRequestValidator : AbstractValidator<SendChatMessageRequest>
{
    public SendChatMessageRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}
