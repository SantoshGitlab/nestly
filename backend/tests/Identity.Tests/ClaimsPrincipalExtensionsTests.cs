using System.Security.Claims;
using FluentAssertions;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.Identity.Tests;

/// <summary>
/// Task 259: the subject-claim reader that replaced 33 copies of
/// <c>Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value)</c>
/// across the consumer, provider and admin controllers.
///
/// Both halves of that expression threw on a token the API should simply
/// reject - <c>!</c> on an absent claim, <c>Guid.Parse</c> on a present but
/// malformed one - and both escaped the action as unhandled exceptions,
/// surfacing as 500 where 401 is the honest answer.
/// </summary>
public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "TestBearer"));

    [Fact]
    public void GetSubjectId_returns_the_subject_claim()
    {
        var id = Guid.NewGuid();

        PrincipalWith(new Claim("sub", id.ToString())).GetSubjectId().Should().Be(id);
    }

    [Fact]
    public void GetSubjectId_falls_back_to_the_mapped_claim_name()
    {
        var id = Guid.NewGuid();

        // A principal built with the default inbound claim mapping still
        // resolves, even though every API turns that mapping off.
        PrincipalWith(new Claim(ClaimTypes.NameIdentifier, id.ToString())).GetSubjectId().Should().Be(id);
    }

    [Fact]
    public void GetSubjectId_throws_the_401_exception_when_the_claim_is_absent()
    {
        var principal = PrincipalWith(new Claim("mobile", "9876543210"));

        principal.Invoking(p => p.GetSubjectId())
            .Should().Throw<MissingSubjectClaimException>(
                "an authenticated token with no subject is a bad token, not a server fault");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    public void GetSubjectId_throws_the_401_exception_when_the_claim_is_malformed(string value)
    {
        var principal = PrincipalWith(new Claim("sub", value));

        principal.Invoking(p => p.GetSubjectId())
            .Should().Throw<MissingSubjectClaimException>();
    }

    [Fact]
    public void TryGetSubjectId_reports_null_rather_than_throwing()
    {
        PrincipalWith().TryGetSubjectId().Should().BeNull();
        PrincipalWith(new Claim("sub", "not-a-guid")).TryGetSubjectId().Should().BeNull();
    }
}
