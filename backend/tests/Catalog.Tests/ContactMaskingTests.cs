using FluentAssertions;
using Nestly.BuildingBlocks.Privacy;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 275: the single masking convention, now that a customer-facing API
/// response depends on it and not just a log line. The edge cases matter
/// more here than they did for logging - a masking function that silently
/// returns its input for some shape of number is a PII leak in an endpoint,
/// where in a log it was only an embarrassing log line.
/// </summary>
public sealed class ContactMaskingTests
{
    [Fact]
    public void Mask_keeps_only_the_last_four_characters()
    {
        ContactMasking.Mask("+919876543210").Should().Be("*********3210");
    }

    [Fact]
    public void Mask_hides_the_country_code_and_every_digit_before_the_last_four()
    {
        var masked = ContactMasking.Mask("+919876543210");

        masked.Should().NotContain("9876543210");
        masked.Should().NotContain("+91");
        masked.Should().EndWith("3210");
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("1", "*")]
    [InlineData("12", "**")]
    [InlineData("1234", "****")]
    public void Mask_hides_a_short_value_entirely(string value, string expected)
    {
        // The boundary that matters: at four characters or fewer, "show the
        // last four" degrades into "show all of it", so the rule flips to
        // masking everything rather than returning the input unchanged.
        ContactMasking.Mask(value).Should().Be(expected);
    }

    [Fact]
    public void Mask_reveals_exactly_one_character_at_the_first_length_above_the_boundary()
    {
        ContactMasking.Mask("12345").Should().Be("*2345");
    }

    [Fact]
    public void Mask_preserves_length_so_the_masked_form_is_never_shorter_than_the_original()
    {
        ContactMasking.Mask("+919876543210").Should().HaveLength("+919876543210".Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskOrNull_returns_null_for_an_absent_contact(string? value)
    {
        // "No number on file" and "a number ending ****" are different
        // claims; a UI that renders a call button off a non-null field must
        // not be handed a row of asterisks for a provider with no phone.
        ContactMasking.MaskOrNull(value).Should().BeNull();
    }

    [Fact]
    public void MaskOrNull_masks_a_present_contact_exactly_like_Mask()
    {
        ContactMasking.MaskOrNull("+919876543210").Should().Be(ContactMasking.Mask("+919876543210"));
    }
}
