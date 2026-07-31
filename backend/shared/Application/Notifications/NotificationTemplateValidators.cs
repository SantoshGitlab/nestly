using FluentValidation;
using Nestly.Domain;

namespace Nestly.Application.Notifications;

public class NotificationTemplateCreateRequestValidator : AbstractValidator<NotificationTemplateCreateRequest>
{
    public NotificationTemplateCreateRequestValidator()
    {
        RuleFor(x => x.TemplateKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Subject).MaximumLength(300);

        RuleFor(x => x.Subject)
            .Empty()
            .When(x => x.Channel == NotificationChannel.Sms)
            .WithMessage("An SMS template cannot have a subject.");

        RuleFor(x => x.Subject)
            .NotEmpty()
            .When(x => x.Channel != NotificationChannel.Sms)
            .WithMessage("Email and push templates require a subject.");
    }
}

public class NotificationTemplateUpdateRequestValidator : AbstractValidator<NotificationTemplateUpdateRequest>
{
    public NotificationTemplateUpdateRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Subject).MaximumLength(300);

        // Channel isn't part of this request's shape (it's immutable - see
        // NotificationTemplate's doc comment), so the Sms/Email-Push subject
        // rule can't be checked here; NotificationTemplate.Update re-validates
        // it against the entity's own stored Channel instead.
    }
}

public class NotificationTemplateAdHocPreviewRequestValidator : AbstractValidator<NotificationTemplateAdHocPreviewRequest>
{
    public NotificationTemplateAdHocPreviewRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Subject).MaximumLength(300);
    }
}
