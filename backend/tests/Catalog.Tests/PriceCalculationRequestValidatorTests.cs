using FluentAssertions;
using Nestly.Application.Pricing;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 48: request shape validation for the price calculation API.</summary>
public sealed class PriceCalculationRequestValidatorTests
{
    private readonly PriceCalculationRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        var request = new PriceCalculationRequest(Guid.NewGuid(), Guid.NewGuid(), 1, [new AddOnSelection(Guid.NewGuid(), 2)]);

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_service_id_fails()
    {
        var request = new PriceCalculationRequest(Guid.Empty, Guid.NewGuid(), 1, []);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Zero_quantity_fails()
    {
        var request = new PriceCalculationRequest(Guid.NewGuid(), Guid.NewGuid(), 0, []);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Zero_addon_quantity_fails()
    {
        var request = new PriceCalculationRequest(Guid.NewGuid(), Guid.NewGuid(), 1, [new AddOnSelection(Guid.NewGuid(), 0)]);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }
}
