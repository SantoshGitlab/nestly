using FluentValidation;

namespace Nestly.Application.Identity;

/// <summary>
/// Strong password policy for admin accounts (SRS 12.1.1, task 95b) — a
/// stricter floor than the customer policy (<c>ResetPasswordRequestValidator</c>'s
/// plain 8-character minimum), because a back-office account is a
/// higher-value target than any single customer's. Shared as a FluentValidation
/// rule so every place an admin password is set — creation, self-service
/// change, Super Admin reset (task 97d) — enforces the same rule instead of
/// each re-implementing it.
/// </summary>
public static class AdminPasswordPolicy
{
    public const int MinimumLength = 12;

    public static IRuleBuilderOptions<T, string> MustBeAStrongPassword<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(MinimumLength).WithMessage($"Password must be at least {MinimumLength} characters long")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character");
}
