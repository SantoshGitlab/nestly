using FluentValidation;

namespace Nestly.Application.AdminRoleManagement;

/// <summary>Bounds a role's name/description to the columns' storage limits (task 313) - mirrors <c>AdminRoleConfiguration</c>'s <c>HasMaxLength</c> calls.</summary>
public class CreateAdminRoleRequestValidator : AbstractValidator<CreateAdminRoleRequest>
{
    public CreateAdminRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).NotNull().MaximumLength(1000);
        RuleFor(x => x.PermissionCodes).NotNull();
        RuleForEach(x => x.PermissionCodes).NotEmpty();
    }
}

public class UpdateAdminRoleRequestValidator : AbstractValidator<UpdateAdminRoleRequest>
{
    public UpdateAdminRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).NotNull().MaximumLength(1000);
    }
}

public class SetAdminRolePermissionsRequestValidator : AbstractValidator<SetAdminRolePermissionsRequest>
{
    public SetAdminRolePermissionsRequestValidator()
    {
        RuleFor(x => x.PermissionCodes).NotNull();
        RuleForEach(x => x.PermissionCodes).NotEmpty();
    }
}
