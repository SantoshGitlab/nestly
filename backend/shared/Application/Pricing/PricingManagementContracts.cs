namespace Nestly.Application.Pricing;

// ---- Base price (SRS 12.8.1 "Base service price") ----

/// <summary>Admin view of a service's base price.</summary>
public sealed record ServicePriceResponse(Guid ServiceId, string ServiceName, decimal Price);

/// <summary>Admin request to change a service's base price.</summary>
public sealed record ServicePriceUpdateRequest(decimal Price);

// ---- Add-on price (SRS 12.8.1 "Add-on price") ----

/// <summary>Admin view of a service add-on's price.</summary>
public sealed record AddOnPriceResponse(Guid AddOnId, Guid ServiceId, string ServiceName, string AddOnName, decimal Price);

/// <summary>Admin request to change an add-on's price.</summary>
public sealed record AddOnPriceUpdateRequest(decimal Price);

// ---- City-wise price (SRS 12.8.1 "City-wise price", 12.8.2 effective dates) ----

/// <summary>Admin view of a city-specific price override, including its effective date range (task 109d).</summary>
public sealed record CityPriceResponse(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    Guid CityId,
    string CityName,
    decimal Price,
    DateOnly EffectiveStartDate,
    DateOnly? EffectiveEndDate);

/// <summary>Admin request to create a city-wise price override.</summary>
public sealed record CityPriceCreateRequest(
    Guid ServiceId,
    Guid CityId,
    decimal Price,
    DateOnly? EffectiveStartDate,
    DateOnly? EffectiveEndDate);

/// <summary>Admin request to update a city-wise price override's amount and/or effective window.</summary>
public sealed record CityPriceUpdateRequest(
    decimal Price,
    DateOnly EffectiveStartDate,
    DateOnly? EffectiveEndDate);

// ---- Promotional price (SRS 12.8.1 "Promotional price") ----

/// <summary>Admin view of a scheduled promotional price.</summary>
public sealed record PromotionalPriceResponse(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    Guid? CityId,
    string? CityName,
    decimal DiscountedPrice,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive);

/// <summary>Admin request to create a promotional price. A null <see cref="CityId"/> applies nationally.</summary>
public sealed record PromotionalPriceCreateRequest(
    Guid ServiceId,
    Guid? CityId,
    decimal DiscountedPrice,
    DateOnly StartDate,
    DateOnly EndDate);

/// <summary>Admin request to update a promotional price's amount and/or date range.</summary>
public sealed record PromotionalPriceUpdateRequest(
    decimal DiscountedPrice,
    DateOnly StartDate,
    DateOnly EndDate);

// ---- City pricing policy: tax + fees (SRS 12.8.1 "Tax configuration", "Visit charge", "Convenience fee") ----

/// <summary>Admin view of a city's tax rate, visit charge, and platform/convenience fee (tasks 109b/109c).</summary>
public sealed record CityPricingPolicyResponse(
    Guid Id,
    Guid CityId,
    string CityName,
    decimal VisitCharge,
    decimal TaxPercentage,
    decimal PlatformFee);

/// <summary>
/// Admin upsert for a city's pricing policy - one policy per city, so
/// create-or-update by <see cref="CityId"/> rather than separate create/update
/// requests (mirrors <see cref="Nestly.Domain.CityPricingPolicy"/>'s own
/// "one row per city" shape).
/// </summary>
public sealed record CityPricingPolicyUpsertRequest(
    decimal VisitCharge,
    decimal TaxPercentage,
    decimal PlatformFee);
