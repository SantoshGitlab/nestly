using FluentAssertions;
using Nestly.Application.Geography;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 360, the master-table half of the bug task 334 fixed on the
/// customer side. <see cref="PincodeCreateRequestValidator"/> allowed any
/// non-empty string up to 10 characters, so an admin could create a master
/// pincode - and map services, providers and localities to it - that no
/// customer address could ever match, because
/// <c>UpsertAddressRequestValidator</c> and <c>ProfileValidators</c> both pin a
/// customer's pincode to <c>^\d{6}$</c>. The area would look serviceable in the
/// admin console and be unreachable from every customer client.
///
/// <para>
/// There is deliberately no update-validator suite alongside this one: a master
/// pincode's code is write-once (<c>IGeographyManagementService</c> exposes
/// create and activate/deactivate only, and the latter two take an id), so this
/// is the single gate the rule has to hold.
/// </para>
/// </summary>
public sealed class PincodeCreateRequestValidatorTests
{
    private readonly PincodeCreateRequestValidator _validator = new();

    private static PincodeCreateRequest Request(string code) => new(Guid.NewGuid(), code);

    [Theory]
    [InlineData("560001")]
    [InlineData("560034")]
    public void Six_digit_code_passes(string code)
    {
        _validator.Validate(Request(code)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]           // NotEmpty
    [InlineData("56003")]      // too short
    [InlineData("5600345")]    // too long
    [InlineData("56003A")]     // not all digits
    [InlineData("ABC1234567")] // the shape the old MaximumLength(10) rule let through
    [InlineData(" 560034")]    // leading whitespace
    [InlineData("560034 ")]    // trailing whitespace
    public void Non_six_digit_code_fails(string code)
    {
        _validator.Validate(Request(code)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_empty_city_is_still_rejected()
    {
        _validator.Validate(new PincodeCreateRequest(Guid.Empty, "560034")).IsValid.Should().BeFalse(
            "tightening the code rule must not have displaced the rule beside it");
    }

    [Fact]
    public void Rejection_message_matches_the_one_the_customer_side_already_uses()
    {
        // The same concept is guarded in three places now (this validator,
        // UpsertAddressRequestValidator, ProfileValidators). Nobody should meet
        // two different explanations of one rule depending on which screen they
        // are on.
        var result = _validator.Validate(Request("ABC1234567"));

        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Pincode must be 6 digits");
    }
}
