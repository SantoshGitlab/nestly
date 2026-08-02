using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Geography;
using Nestly.Infrastructure;

namespace Nestly.PartnerApi.Controllers;

/// <summary>
/// Read-only city/zone/pincode lookup for the partner service-areas picker
/// (task 205, PARTNER.md's Capability &amp; Coverage domain). Before this
/// controller, <c>ProfileController</c>'s service-areas endpoints took bare
/// <c>cityId</c>/<c>zoneId</c>/<c>pincodeId</c> with no lookup to resolve
/// them against - partner-web's <c>ServiceAreasSection</c> had partners
/// hand-type raw GUIDs. Reuses the existing admin-facing
/// <see cref="IGeographyManagementService"/> (same service
/// <c>AdminApi.Controllers.GeographyController</c> calls) rather than adding
/// a new query service - only the response shape is new, trimmed to
/// id/name (matching <see cref="Nestly.Application.PartnerProfile.PartnerServiceAreaInput"/>'s
/// cityId/zoneId/pincodeId shape).
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize(AuthenticationSchemes = DependencyInjection.PartnerJwtBearerScheme)]
[Route("api/v{version:apiVersion}/geography")]
public class GeographyController : ControllerBase
{
    private readonly IGeographyManagementService _geographyManagementService;

    public GeographyController(IGeographyManagementService geographyManagementService)
    {
        _geographyManagementService = geographyManagementService;
    }

    /// <summary>Active cities, for the service-areas picker's city dropdown.</summary>
    [HttpGet("cities")]
    [ProducesResponseType(typeof(IReadOnlyList<PartnerGeographyCityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCities()
    {
        var cities = await _geographyManagementService.ListCitiesAsync(stateId: null);
        var response = cities
            .Where(c => c.IsActive)
            .Select(c => new PartnerGeographyCityResponse(c.Id, c.Name))
            .ToList();

        return Ok(response);
    }

    /// <summary>Active zones, optionally filtered to one city, for the service-areas picker's zone dropdown.</summary>
    [HttpGet("zones")]
    [ProducesResponseType(typeof(IReadOnlyList<PartnerGeographyZoneResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListZones([FromQuery] Guid? cityId)
    {
        var zones = await _geographyManagementService.ListZonesAsync(cityId);
        var response = zones
            .Where(z => z.IsActive)
            .Select(z => new PartnerGeographyZoneResponse(z.Id, z.CityId, z.Name))
            .ToList();

        return Ok(response);
    }

    /// <summary>Active pincodes, optionally filtered to one city, for the service-areas picker's pincode dropdown.</summary>
    [HttpGet("pincodes")]
    [ProducesResponseType(typeof(IReadOnlyList<PartnerGeographyPincodeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPincodes([FromQuery] Guid? cityId)
    {
        var pincodes = await _geographyManagementService.ListPincodesAsync(cityId);
        var response = pincodes
            .Where(p => p.IsActive)
            .Select(p => new PartnerGeographyPincodeResponse(p.Id, p.CityId, p.Code))
            .ToList();

        return Ok(response);
    }
}

/// <summary>City name lookup entry - deliberately just id/name, unlike the admin-facing <see cref="CityAdminResponse"/>.</summary>
public sealed record PartnerGeographyCityResponse(Guid Id, string Name);

/// <summary>Zone name lookup entry - deliberately just id/cityId/name, unlike the admin-facing <see cref="ZoneResponse"/>.</summary>
public sealed record PartnerGeographyZoneResponse(Guid Id, Guid CityId, string Name);

/// <summary>Pincode lookup entry (code doubles as its display name) - deliberately just id/cityId/code, unlike the admin-facing <see cref="PincodeAdminResponse"/>.</summary>
public sealed record PartnerGeographyPincodeResponse(Guid Id, Guid CityId, string Code);
