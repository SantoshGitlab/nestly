using FluentAssertions;
using FluentValidation;
using Nestly.Application.Identity;

namespace Nestly.Identity.Tests;

/// <summary>
/// Strong password policy for admin accounts (SRS 12.1.1, task 95b). No admin
/// password-set/reset endpoint exists yet (that's task 97d) — this validates
/// the reusable rule itself so it is proven correct and ready for that task
/// (and task 97a's admin-user creation) to apply.
/// </summary>
public class AdminPasswordPolicyTests
{
    private sealed record PasswordHolder(string Password);

    private sealed class PasswordHolderValidator : AbstractValidator<PasswordHolder>
    {
        public PasswordHolderValidator()
        {
            RuleFor(x => x.Password).MustBeAStrongPassword();
        }
    }

    private static readonly PasswordHolderValidator Validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("short1!A")]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoDigitsHere!!")]
    [InlineData("NoSpecialChars123")]
    public void Rejects_passwords_that_do_not_meet_the_policy(string password)
    {
        Validator.Validate(new PasswordHolder(password)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Str0ng!Passw0rd")]
    [InlineData("Another$Valid1Pass")]
    public void Accepts_passwords_that_meet_every_rule(string password)
    {
        Validator.Validate(new PasswordHolder(password)).IsValid.Should().BeTrue();
    }
}
