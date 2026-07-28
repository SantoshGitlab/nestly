namespace Nestly.Application.Addresses;

/// <summary>Shared by create and update — SRS 11.3.2 address fields.</summary>
public record UpsertAddressRequest(
    string Label,
    string Line1,
    string? Line2,
    string? Landmark,
    string Pincode,
    string City,
    string State,
    decimal Latitude,
    decimal Longitude,
    string ContactName,
    string ContactMobile,
    bool IsDefault);

public record CustomerAddressResponse(
    Guid Id,
    string Label,
    string Line1,
    string? Line2,
    string? Landmark,
    string Pincode,
    string City,
    string State,
    decimal Latitude,
    decimal Longitude,
    string ContactName,
    string ContactMobile,
    bool IsDefault);
