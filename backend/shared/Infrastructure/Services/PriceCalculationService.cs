using Nestly.Application;
using Nestly.Application.Pricing;
using Nestly.Application.Serviceability;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Server-side price calculation (tasks 47a-c, 48). Base + add-ons +
/// quantity (47a-c) come straight off Service/ServiceAddOn; city-wise price
/// and visit charge/tax/fee (47d-g) apply on top. Coupons, wallet
/// deduction, and cancellation/reschedule fees (SRS 11.9.1) are out of
/// scope here - they belong to Payments/Post-Booking (Phase 4/5), which
/// don't exist yet.
/// </summary>
public class PriceCalculationService : IPriceCalculationService
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceAddOnRepository _addOnRepository;
    private readonly IServiceabilityRepository _serviceabilityRepository;
    private readonly IServiceCityPriceRepository _cityPriceRepository;
    private readonly ICityPricingPolicyRepository _pricingPolicyRepository;

    public PriceCalculationService(
        IServiceRepository serviceRepository,
        IServiceAddOnRepository addOnRepository,
        IServiceabilityRepository serviceabilityRepository,
        IServiceCityPriceRepository cityPriceRepository,
        ICityPricingPolicyRepository pricingPolicyRepository)
    {
        _serviceRepository = serviceRepository;
        _addOnRepository = addOnRepository;
        _serviceabilityRepository = serviceabilityRepository;
        _cityPriceRepository = cityPriceRepository;
        _pricingPolicyRepository = pricingPolicyRepository;
    }

    public async Task<Result<PriceBreakdownResponse>> CalculateAsync(PriceCalculationRequest request)
    {
        if (request.Quantity <= 0)
        {
            return Error.Validation("Pricing.InvalidQuantity", "Quantity must be positive.");
        }

        var service = await _serviceRepository.GetByIdAsync(request.ServiceId);
        if (service is null || !service.IsActive)
        {
            return Error.NotFound("Pricing.ServiceNotFound", "The specified service does not exist.");
        }

        if (!await _serviceabilityRepository.CityExistsAsync(request.CityId))
        {
            return Error.NotFound("Pricing.CityNotFound", "The specified city does not exist.");
        }

        var addOnLineItems = new List<AddOnLineItem>(request.AddOns.Count);
        foreach (var selection in request.AddOns)
        {
            if (selection.Quantity <= 0)
            {
                return Error.Validation("Pricing.InvalidAddOnQuantity", "Add-on quantity must be positive.");
            }

            var addOn = await _addOnRepository.GetByIdAsync(selection.AddOnId);
            if (addOn is null || !addOn.IsActive || addOn.ServiceId != request.ServiceId)
            {
                return Error.Validation("Pricing.InvalidAddOn", $"Add-on {selection.AddOnId} is not available for this service.");
            }

            decimal lineTotal = addOn.Price * selection.Quantity;
            addOnLineItems.Add(new AddOnLineItem(addOn.Id, addOn.Name, addOn.Price, selection.Quantity, lineTotal));
        }

        var cityOverride = await _cityPriceRepository.GetForServiceAndCityAsync(request.ServiceId, request.CityId);
        decimal basePrice = cityOverride?.Price ?? service.Price;
        decimal baseTotal = basePrice * request.Quantity;
        decimal addOnTotal = addOnLineItems.Sum(a => a.LineTotal);

        var pricingPolicy = await _pricingPolicyRepository.GetByCityAsync(request.CityId);
        decimal visitCharge = pricingPolicy?.VisitCharge ?? 0m;
        decimal taxPercentage = pricingPolicy?.TaxPercentage ?? 0m;
        decimal platformFee = pricingPolicy?.PlatformFee ?? 0m;

        decimal subtotal = baseTotal + addOnTotal + visitCharge;
        decimal taxAmount = Math.Round(subtotal * taxPercentage / 100m, 2);
        decimal totalPayable = subtotal + taxAmount + platformFee;

        return new PriceBreakdownResponse(
            basePrice,
            request.Quantity,
            baseTotal,
            addOnLineItems,
            addOnTotal,
            visitCharge,
            subtotal,
            taxPercentage,
            taxAmount,
            platformFee,
            totalPayable);
    }
}
