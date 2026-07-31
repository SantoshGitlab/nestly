using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Pricing;

/// <summary>
/// Admin CRUD over pricing (SRS 12.8: base/add-on/city-wise/promotional
/// price, tax, visit charge, platform fee, effective dating, price-change
/// audit - tasks 109a-109e). Consumer-facing price calculation stays in
/// <see cref="IPriceCalculationService"/>; this service only manages the
/// admin-configurable data those calculations read from.
/// </summary>
public interface IPricingManagementService
{
    // Base price

    Task<IReadOnlyList<ServicePriceResponse>> ListServicePricesAsync(CancellationToken cancellationToken = default);

    Task<Result<ServicePriceResponse>> UpdateServicePriceAsync(Guid serviceId, ServicePriceUpdateRequest request, CancellationToken cancellationToken = default);

    // Add-on price

    Task<IReadOnlyList<AddOnPriceResponse>> ListAddOnPricesAsync(Guid? serviceId, CancellationToken cancellationToken = default);

    Task<Result<AddOnPriceResponse>> UpdateAddOnPriceAsync(Guid addOnId, AddOnPriceUpdateRequest request, CancellationToken cancellationToken = default);

    // City-wise price

    Task<IReadOnlyList<CityPriceResponse>> ListCityPricesAsync(Guid? serviceId, Guid? cityId, CancellationToken cancellationToken = default);

    Task<Result<CityPriceResponse>> CreateCityPriceAsync(CityPriceCreateRequest request, CancellationToken cancellationToken = default);

    Task<Result<CityPriceResponse>> UpdateCityPriceAsync(Guid id, CityPriceUpdateRequest request, CancellationToken cancellationToken = default);

    // Promotional price

    Task<IReadOnlyList<PromotionalPriceResponse>> ListPromotionalPricesAsync(Guid? serviceId, CancellationToken cancellationToken = default);

    Task<Result<PromotionalPriceResponse>> CreatePromotionalPriceAsync(PromotionalPriceCreateRequest request, CancellationToken cancellationToken = default);

    Task<Result<PromotionalPriceResponse>> UpdatePromotionalPriceAsync(Guid id, PromotionalPriceUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result> SetPromotionalPriceActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    // City pricing policy: tax + fees

    Task<IReadOnlyList<CityPricingPolicyResponse>> ListCityPricingPoliciesAsync(CancellationToken cancellationToken = default);

    Task<Result<CityPricingPolicyResponse>> UpsertCityPricingPolicyAsync(Guid cityId, CityPricingPolicyUpsertRequest request, CancellationToken cancellationToken = default);
}
