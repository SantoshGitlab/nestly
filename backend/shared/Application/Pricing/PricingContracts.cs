namespace Nestly.Application.Pricing;

public record AddOnSelection(Guid AddOnId, int Quantity);

/// <summary>Server-side price calculation input (task 48, SRS 11.9.2 - final price must be calculated server-side).</summary>
public record PriceCalculationRequest(Guid ServiceId, Guid CityId, int Quantity, IReadOnlyList<AddOnSelection> AddOns);

public record AddOnLineItem(Guid AddOnId, string Name, decimal UnitPrice, int Quantity, decimal LineTotal);

/// <summary>Full price breakdown (SRS 11.9.1): base, add-ons, quantity, city-wise price, visit charge, tax, fees.</summary>
public record PriceBreakdownResponse(
    decimal BasePrice,
    int Quantity,
    decimal BaseTotal,
    IReadOnlyList<AddOnLineItem> AddOnLineItems,
    decimal AddOnTotal,
    decimal VisitCharge,
    decimal Subtotal,
    decimal TaxPercentage,
    decimal TaxAmount,
    decimal PlatformFee,
    decimal TotalPayable);
