using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Pricing;

/// <summary>Server-side authoritative price calculation (task 48, SRS 11.9.2).</summary>
public interface IPriceCalculationService
{
    Task<Result<PriceBreakdownResponse>> CalculateAsync(PriceCalculationRequest request);
}
