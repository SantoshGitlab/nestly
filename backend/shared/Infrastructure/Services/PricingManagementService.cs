using System.Text.Json;
using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Pricing;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Admin CRUD over pricing (SRS 12.8, tasks 109a-109e): base/add-on/city-wise/
/// promotional price, per-city tax rate, visit charge and platform fee, and
/// their effective dating. Every write is audited via
/// <see cref="IAuditLogWriter"/> (task 109e), following the same
/// serialize-before/after-JSON pattern as <c>SystemSettingsService</c> - a
/// price change is exactly the kind of critical admin action SRS 12.8.2
/// requires an audit trail for.
///
/// This service only manages the admin-configurable pricing data; it does
/// not change how <see cref="IPriceCalculationService"/> reads it (that is a
/// separate, already-implemented consumer-facing calculation path - tasks
/// 47a-48).
/// </summary>
public class PricingManagementService : IPricingManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceAddOnRepository _addOnRepository;
    private readonly ICityRepository _cityRepository;
    private readonly IServiceCityPriceRepository _cityPriceRepository;
    private readonly IPromotionalPriceRepository _promotionalPriceRepository;
    private readonly ICityPricingPolicyRepository _pricingPolicyRepository;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IAuditContextProvider _auditContextProvider;

    public PricingManagementService(
        IServiceRepository serviceRepository,
        IServiceAddOnRepository addOnRepository,
        ICityRepository cityRepository,
        IServiceCityPriceRepository cityPriceRepository,
        IPromotionalPriceRepository promotionalPriceRepository,
        ICityPricingPolicyRepository pricingPolicyRepository,
        IAuditLogWriter auditLogWriter,
        IAuditContextProvider auditContextProvider)
    {
        _serviceRepository = serviceRepository;
        _addOnRepository = addOnRepository;
        _cityRepository = cityRepository;
        _cityPriceRepository = cityPriceRepository;
        _promotionalPriceRepository = promotionalPriceRepository;
        _pricingPolicyRepository = pricingPolicyRepository;
        _auditLogWriter = auditLogWriter;
        _auditContextProvider = auditContextProvider;
    }

    // ---- Base price ----

    public async Task<IReadOnlyList<ServicePriceResponse>> ListServicePricesAsync(CancellationToken cancellationToken = default)
    {
        var services = await _serviceRepository.ListAllAsync();
        return services.Select(s => new ServicePriceResponse(s.Id, s.Name, s.Price)).ToList();
    }

    public async Task<Result<ServicePriceResponse>> UpdateServicePriceAsync(
        Guid serviceId, ServicePriceUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetByIdAsync(serviceId);
        if (service is null)
        {
            return Error.NotFound("Pricing.ServiceNotFound", "The specified service does not exist.");
        }

        decimal oldPrice = service.Price;
        service.SetPrice(request.Price);

        await WriteAuditAsync("Service", serviceId, "PriceChanged", new { Price = oldPrice }, new { service.Price }, cancellationToken);
        await _serviceRepository.UpdateAsync(service);

        return new ServicePriceResponse(service.Id, service.Name, service.Price);
    }

    // ---- Add-on price ----

    public async Task<IReadOnlyList<AddOnPriceResponse>> ListAddOnPricesAsync(Guid? serviceId, CancellationToken cancellationToken = default)
    {
        var addOns = await _addOnRepository.ListAllAsync(serviceId);
        var serviceNames = await BuildServiceNameLookupAsync();
        return addOns.Select(a => new AddOnPriceResponse(
            a.Id, a.ServiceId, serviceNames.GetValueOrDefault(a.ServiceId, string.Empty), a.Name, a.Price)).ToList();
    }

    public async Task<Result<AddOnPriceResponse>> UpdateAddOnPriceAsync(
        Guid addOnId, AddOnPriceUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var addOn = await _addOnRepository.GetByIdAsync(addOnId);
        if (addOn is null)
        {
            return Error.NotFound("Pricing.AddOnNotFound", "The specified add-on does not exist.");
        }

        decimal oldPrice = addOn.Price;
        addOn.SetPrice(request.Price);

        await WriteAuditAsync("ServiceAddOn", addOnId, "PriceChanged", new { Price = oldPrice }, new { addOn.Price }, cancellationToken);
        await _addOnRepository.UpdateAsync(addOn);

        var service = await _serviceRepository.GetByIdAsync(addOn.ServiceId);
        return new AddOnPriceResponse(addOn.Id, addOn.ServiceId, service?.Name ?? string.Empty, addOn.Name, addOn.Price);
    }

    // ---- City-wise price ----

    public async Task<IReadOnlyList<CityPriceResponse>> ListCityPricesAsync(
        Guid? serviceId, Guid? cityId, CancellationToken cancellationToken = default)
    {
        var prices = await _cityPriceRepository.ListAsync(serviceId, cityId);
        var serviceNames = await BuildServiceNameLookupAsync();
        var cityNames = await BuildCityNameLookupAsync();
        return prices.Select(p => ToResponse(p, serviceNames, cityNames)).ToList();
    }

    public async Task<Result<CityPriceResponse>> CreateCityPriceAsync(
        CityPriceCreateRequest request, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetByIdAsync(request.ServiceId);
        if (service is null)
        {
            return Error.NotFound("Pricing.ServiceNotFound", "The specified service does not exist.");
        }

        var city = await _cityRepository.GetByIdAsync(request.CityId);
        if (city is null)
        {
            return Error.NotFound("Pricing.CityNotFound", "The specified city does not exist.");
        }

        if (await _cityPriceRepository.GetForServiceAndCityAsync(request.ServiceId, request.CityId) is not null)
        {
            return Error.Conflict("Pricing.DuplicateCityPrice", "A city price override already exists for this service and city.");
        }

        var cityPrice = new ServiceCityPrice(
            Guid.NewGuid(), request.ServiceId, request.CityId, request.Price, request.EffectiveStartDate, request.EffectiveEndDate);

        await WriteAuditAsync("ServiceCityPrice", cityPrice.Id, "Created", oldValue: null, new
        {
            cityPrice.ServiceId,
            cityPrice.CityId,
            cityPrice.Price,
            cityPrice.EffectiveStartDate,
            cityPrice.EffectiveEndDate
        }, cancellationToken);
        await _cityPriceRepository.AddAsync(cityPrice);

        return new CityPriceResponse(
            cityPrice.Id, service.Id, service.Name, city.Id, city.Name,
            cityPrice.Price, cityPrice.EffectiveStartDate, cityPrice.EffectiveEndDate);
    }

    public async Task<Result<CityPriceResponse>> UpdateCityPriceAsync(
        Guid id, CityPriceUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var cityPrice = await _cityPriceRepository.GetByIdAsync(id);
        if (cityPrice is null)
        {
            return Error.NotFound("Pricing.CityPriceNotFound", "The specified city price override does not exist.");
        }

        var oldValue = new
        {
            cityPrice.Price,
            cityPrice.EffectiveStartDate,
            cityPrice.EffectiveEndDate
        };

        cityPrice.SetPrice(request.Price);
        cityPrice.SetEffectiveDateRange(request.EffectiveStartDate, request.EffectiveEndDate);

        await WriteAuditAsync("ServiceCityPrice", id, "Updated", oldValue, new
        {
            cityPrice.Price,
            cityPrice.EffectiveStartDate,
            cityPrice.EffectiveEndDate
        }, cancellationToken);
        await _cityPriceRepository.UpdateAsync(cityPrice);

        var service = await _serviceRepository.GetByIdAsync(cityPrice.ServiceId);
        var city = await _cityRepository.GetByIdAsync(cityPrice.CityId);
        return new CityPriceResponse(
            cityPrice.Id, cityPrice.ServiceId, service?.Name ?? string.Empty, cityPrice.CityId, city?.Name ?? string.Empty,
            cityPrice.Price, cityPrice.EffectiveStartDate, cityPrice.EffectiveEndDate);
    }

    // ---- Promotional price ----

    public async Task<IReadOnlyList<PromotionalPriceResponse>> ListPromotionalPricesAsync(
        Guid? serviceId, CancellationToken cancellationToken = default)
    {
        var promotions = await _promotionalPriceRepository.ListAsync(serviceId);
        var serviceNames = await BuildServiceNameLookupAsync();
        var cityNames = await BuildCityNameLookupAsync();
        return promotions.Select(p => ToResponse(p, serviceNames, cityNames)).ToList();
    }

    public async Task<Result<PromotionalPriceResponse>> CreatePromotionalPriceAsync(
        PromotionalPriceCreateRequest request, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetByIdAsync(request.ServiceId);
        if (service is null)
        {
            return Error.NotFound("Pricing.ServiceNotFound", "The specified service does not exist.");
        }

        City? city = null;
        if (request.CityId.HasValue)
        {
            city = await _cityRepository.GetByIdAsync(request.CityId.Value);
            if (city is null)
            {
                return Error.NotFound("Pricing.CityNotFound", "The specified city does not exist.");
            }
        }

        var promotion = new PromotionalPrice(
            Guid.NewGuid(), request.ServiceId, request.CityId, request.DiscountedPrice, request.StartDate, request.EndDate);

        await WriteAuditAsync("PromotionalPrice", promotion.Id, "Created", oldValue: null, new
        {
            promotion.ServiceId,
            promotion.CityId,
            promotion.DiscountedPrice,
            promotion.StartDate,
            promotion.EndDate
        }, cancellationToken);
        await _promotionalPriceRepository.AddAsync(promotion);

        return new PromotionalPriceResponse(
            promotion.Id, service.Id, service.Name, city?.Id, city?.Name,
            promotion.DiscountedPrice, promotion.StartDate, promotion.EndDate, promotion.IsActive);
    }

    public async Task<Result<PromotionalPriceResponse>> UpdatePromotionalPriceAsync(
        Guid id, PromotionalPriceUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var promotion = await _promotionalPriceRepository.GetByIdAsync(id);
        if (promotion is null)
        {
            return Error.NotFound("Pricing.PromotionalPriceNotFound", "The specified promotional price does not exist.");
        }

        var oldValue = new { promotion.DiscountedPrice, promotion.StartDate, promotion.EndDate };

        promotion.SetDiscountedPrice(request.DiscountedPrice);
        promotion.SetDateRange(request.StartDate, request.EndDate);

        await WriteAuditAsync("PromotionalPrice", id, "Updated", oldValue,
            new { promotion.DiscountedPrice, promotion.StartDate, promotion.EndDate }, cancellationToken);
        await _promotionalPriceRepository.UpdateAsync(promotion);

        var service = await _serviceRepository.GetByIdAsync(promotion.ServiceId);
        var city = promotion.CityId.HasValue ? await _cityRepository.GetByIdAsync(promotion.CityId.Value) : null;
        return new PromotionalPriceResponse(
            promotion.Id, promotion.ServiceId, service?.Name ?? string.Empty, promotion.CityId, city?.Name,
            promotion.DiscountedPrice, promotion.StartDate, promotion.EndDate, promotion.IsActive);
    }

    public async Task<Result> SetPromotionalPriceActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var promotion = await _promotionalPriceRepository.GetByIdAsync(id);
        if (promotion is null)
        {
            return Result.Failure(Error.NotFound("Pricing.PromotionalPriceNotFound", "The specified promotional price does not exist."));
        }

        if (isActive) promotion.Activate(); else promotion.Deactivate();

        await WriteAuditAsync("PromotionalPrice", id, isActive ? "Activated" : "Deactivated", oldValue: null, newValue: null, cancellationToken);
        await _promotionalPriceRepository.UpdateAsync(promotion);

        return Result.Success();
    }

    // ---- City pricing policy: tax + fees ----

    public async Task<IReadOnlyList<CityPricingPolicyResponse>> ListCityPricingPoliciesAsync(CancellationToken cancellationToken = default)
    {
        var policies = await _pricingPolicyRepository.ListAsync();
        var cityNames = await BuildCityNameLookupAsync();
        return policies.Select(p => new CityPricingPolicyResponse(
            p.Id, p.CityId, cityNames.GetValueOrDefault(p.CityId, string.Empty), p.VisitCharge, p.TaxPercentage, p.PlatformFee)).ToList();
    }

    public async Task<Result<CityPricingPolicyResponse>> UpsertCityPricingPolicyAsync(
        Guid cityId, CityPricingPolicyUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var city = await _cityRepository.GetByIdAsync(cityId);
        if (city is null)
        {
            return Error.NotFound("Pricing.CityNotFound", "The specified city does not exist.");
        }

        var policy = await _pricingPolicyRepository.GetByCityAsync(cityId);
        if (policy is null)
        {
            policy = new CityPricingPolicy(Guid.NewGuid(), cityId, request.VisitCharge, request.TaxPercentage, request.PlatformFee);

            await WriteAuditAsync("CityPricingPolicy", policy.Id, "Created", oldValue: null, new
            {
                policy.CityId,
                policy.VisitCharge,
                policy.TaxPercentage,
                policy.PlatformFee
            }, cancellationToken);
            await _pricingPolicyRepository.AddAsync(policy);
        }
        else
        {
            var oldValue = new { policy.VisitCharge, policy.TaxPercentage, policy.PlatformFee };

            policy.SetVisitCharge(request.VisitCharge);
            policy.SetTaxPercentage(request.TaxPercentage);
            policy.SetPlatformFee(request.PlatformFee);

            await WriteAuditAsync("CityPricingPolicy", policy.Id, "Updated", oldValue,
                new { policy.VisitCharge, policy.TaxPercentage, policy.PlatformFee }, cancellationToken);
            await _pricingPolicyRepository.UpdateAsync(policy);
        }

        return new CityPricingPolicyResponse(policy.Id, cityId, city.Name, policy.VisitCharge, policy.TaxPercentage, policy.PlatformFee);
    }

    // ---- Helpers ----

    private async Task<Dictionary<Guid, string>> BuildServiceNameLookupAsync() =>
        (await _serviceRepository.ListAllAsync()).ToDictionary(s => s.Id, s => s.Name);

    private async Task<Dictionary<Guid, string>> BuildCityNameLookupAsync() =>
        (await _cityRepository.ListAsync(null)).ToDictionary(c => c.Id, c => c.Name);

    private static CityPriceResponse ToResponse(
        ServiceCityPrice price, IReadOnlyDictionary<Guid, string> serviceNames, IReadOnlyDictionary<Guid, string> cityNames) =>
        new(
            price.Id,
            price.ServiceId,
            serviceNames.GetValueOrDefault(price.ServiceId, string.Empty),
            price.CityId,
            cityNames.GetValueOrDefault(price.CityId, string.Empty),
            price.Price,
            price.EffectiveStartDate,
            price.EffectiveEndDate);

    private static PromotionalPriceResponse ToResponse(
        PromotionalPrice promotion, IReadOnlyDictionary<Guid, string> serviceNames, IReadOnlyDictionary<Guid, string> cityNames) =>
        new(
            promotion.Id,
            promotion.ServiceId,
            serviceNames.GetValueOrDefault(promotion.ServiceId, string.Empty),
            promotion.CityId,
            promotion.CityId.HasValue ? cityNames.GetValueOrDefault(promotion.CityId.Value) : null,
            promotion.DiscountedPrice,
            promotion.StartDate,
            promotion.EndDate,
            promotion.IsActive);

    /// <summary>
    /// Serializes and writes one price-change audit entry (SRS 12.8.2, task
    /// 109e). Staged into the same <c>DbContext</c> change tracker as the
    /// entity write above it - the repository's own <c>SaveChangesAsync</c>
    /// commits both in one transaction (<see cref="IAuditLogWriter.WriteAsync"/>'s
    /// documented contract).
    /// </summary>
    private async Task WriteAuditAsync(
        string entityName, Guid entityId, string action, object? oldValue, object? newValue, CancellationToken cancellationToken)
    {
        string? oldJson = oldValue is null ? null : JsonSerializer.Serialize(oldValue, JsonOptions);
        string? newJson = newValue is null ? null : JsonSerializer.Serialize(newValue, JsonOptions);

        await _auditLogWriter.WriteAsync(
            new AuditEntry(entityName, entityId.ToString(), action, oldJson, newJson),
            cancellationToken);
    }
}
