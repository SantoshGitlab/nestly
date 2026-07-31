using FluentValidation;
using Nestly.Application.Identity;

namespace Nestly.Application.AdminUserManagement;

/// <summary>Bounds paging so a caller cannot request an unbounded or negative page (task 97a), same rule as <c>CustomerSearchRequestValidator</c>.</summary>
public class AdminUserSearchRequestValidator : AbstractValidator<AdminUserSearchRequest>
{
    public const int MaxPageSize = 100;

    public AdminUserSearchRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}

/// <summary>
/// Validates a new admin account (task 97a). The password rule is shared
/// with the self-service admin login path via <see cref="AdminPasswordPolicy"/>
/// rather than re-implemented here.
/// </summary>
public class CreateAdminUserRequestValidator : AbstractValidator<CreateAdminUserRequest>
{
    public CreateAdminUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).MustBeAStrongPassword();
    }
}

public class UpdateAdminUserRequestValidator : AbstractValidator<UpdateAdminUserRequest>
{
    public UpdateAdminUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
    }
}
