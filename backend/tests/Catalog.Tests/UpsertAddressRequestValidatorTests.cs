using FluentAssertions;
using Nestly.Application.Addresses;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 334: the pincode rule here and customer-web's AddressForm
/// (<c>z.string().regex(/^\d{6}$/)</c>) disagreed - the backend accepted any
/// non-empty string up to 12 characters, so a value the form rejected could
/// still be persisted through any other client, and a serviceable area whose
/// pincode did not match the form's shape was unaddressable from the web app.
/// These tests pin the backend to the stricter of the two.
/// </summary>
public sealed class UpsertAddressRequestValidatorTests
{
    private readonly UpsertAddressRequestValidator _validator = new();

    private static UpsertAddressRequest Request(string pincode) => new(
        Label: "Home",
        Line1: "221B Baker Street",
        Line2: null,
        Landmark: null,
        Pincode: pincode,
        City: "Bengaluru",
        State: "Karnataka",
        Latitude: 12.9716m,
        Longitude: 77.5946m,
        ContactName: "Test Customer",
        ContactMobile: "+919876543210",
        IsDefault: false);

    [Fact]
    public void Six_digit_pincode_passes()
    {
        _validator.Validate(Request("560034")).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]           // NotEmpty
    [InlineData("56003")]      // too short
    [InlineData("5600345")]    // too long
    [InlineData("56003A")]     // not all digits
    [InlineData("ABC123")]     // the shape the old MaximumLength(12) rule let through
    [InlineData(" 560034")]    // leading whitespace
    [InlineData("560034 ")]    // trailing whitespace
    public void Non_six_digit_pincode_fails(string pincode)
    {
        _validator.Validate(Request(pincode)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rejection_message_matches_the_one_ProfileValidators_already_uses()
    {
        // The two validators guard the same concept; a customer should not see
        // two different explanations for the same rule depending on which
        // screen they are on.
        var result = _validator.Validate(Request("ABC123"));

        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Pincode must be 6 digits");
    }
}
