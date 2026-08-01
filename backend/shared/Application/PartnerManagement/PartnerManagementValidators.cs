using FluentValidation;
using Nestly.Domain;

namespace Nestly.Application.PartnerManagement;

/// <summary>Bounds paging, mirroring <c>CustomerSearchRequestValidator</c> (task 150a).</summary>
public class PartnerSearchRequestValidator : AbstractValidator<PartnerSearchRequest>
{
    public const int MaxPageSize = 100;

    public PartnerSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}

public class CreatePartnerRequestValidator : AbstractValidator<CreatePartnerRequest>
{
    public CreatePartnerRequestValidator()
    {
        RuleFor(x => x.LegalName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class UpdatePartnerRequestValidator : AbstractValidator<UpdatePartnerRequest>
{
    public UpdatePartnerRequestValidator()
    {
        RuleFor(x => x.LegalName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class SuspendPartnerRequestValidator : AbstractValidator<SuspendPartnerRequest>
{
    public SuspendPartnerRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public class RejectPartnerKycDocumentRequestValidator : AbstractValidator<RejectPartnerKycDocumentRequest>
{
    public RejectPartnerKycDocumentRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public class RecordBackgroundCheckRequestValidator : AbstractValidator<RecordBackgroundCheckRequest>
{
    public RecordBackgroundCheckRequestValidator()
    {
        RuleFor(x => x.Status).NotEqual(PartnerBackgroundCheckStatus.Pending)
            .WithMessage("A background check must be recorded with a final Passed/Failed outcome.");
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class AssignPartnerRequestValidator : AbstractValidator<AssignPartnerRequest>
{
    public AssignPartnerRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty();
    }
}

public class RejectAssignmentRequestValidator : AbstractValidator<RejectAssignmentRequest>
{
    public RejectAssignmentRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}

public class RecordPartnerEarningAdjustmentRequestValidator : AbstractValidator<RecordPartnerEarningAdjustmentRequest>
{
    public RecordPartnerEarningAdjustmentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
    }
}

public class CreatePartnerPayoutRequestValidator : AbstractValidator<CreatePartnerPayoutRequest>
{
    public CreatePartnerPayoutRequestValidator()
    {
        RuleFor(x => x.PeriodEnd)
            .GreaterThanOrEqualTo(x => x.PeriodStart)
            .WithMessage("Payout period end cannot be before its start.");
    }
}

public class UpdatePartnerPayoutStatusRequestValidator : AbstractValidator<UpdatePartnerPayoutStatusRequest>
{
    public UpdatePartnerPayoutStatusRequestValidator()
    {
        RuleFor(x => x.PayoutReference).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
