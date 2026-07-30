namespace Nestly.Application.Serviceability;

/// <summary>Whether a category or service can be booked at the checked geography (SRS 11.4.2, 12.9.2).</summary>
public record ServiceabilityResponse(bool IsServiceable);
