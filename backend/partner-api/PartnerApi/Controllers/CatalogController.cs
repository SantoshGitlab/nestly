using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Catalog;
using Nestly.Infrastructure;

namespace Nestly.PartnerApi.Controllers;

/// <summary>
/// Read-only category/service lookup for the partner skills picker (task 205,
/// PARTNER.md's Capability &amp; Coverage domain). Before this controller,
/// <c>ProfileController</c>'s skills endpoints took a bare <c>categoryId</c>/
/// <c>serviceId</c> with no lookup to resolve them against - partner-web's
/// <c>SkillsSection</c> had partners hand-type raw GUIDs. This reuses the
/// existing admin-facing <see cref="ICategoryManagementService"/>/
/// <see cref="IServiceManagementService"/> (same services
/// <c>AdminApi.Controllers.CategoriesController</c>/<c>ServicesController</c>
/// call) rather than adding new query services - only the response shape is
/// new, trimmed to id/name (no SEO/media/pricing fields an admin screen needs
/// but a partner picker never should see).
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize(AuthenticationSchemes = DependencyInjection.PartnerJwtBearerScheme)]
[Route("api/v{version:apiVersion}/catalog")]
public class CatalogController : ControllerBase
{
    private readonly ICategoryManagementService _categoryManagementService;
    private readonly IServiceManagementService _serviceManagementService;

    public CatalogController(
        ICategoryManagementService categoryManagementService,
        IServiceManagementService serviceManagementService)
    {
        _categoryManagementService = categoryManagementService;
        _serviceManagementService = serviceManagementService;
    }

    /// <summary>Active categories, for the skills picker's category dropdown.</summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<PartnerCatalogCategoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCategories()
    {
        var categories = await _categoryManagementService.ListAsync();
        var response = categories
            .Where(c => c.IsActive)
            .Select(c => new PartnerCatalogCategoryResponse(c.Id, c.Name))
            .ToList();

        return Ok(response);
    }

    /// <summary>Active services, optionally filtered to one category, for the skills picker's service dropdown.</summary>
    [HttpGet("services")]
    [ProducesResponseType(typeof(IReadOnlyList<PartnerCatalogServiceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListServices([FromQuery] Guid? categoryId)
    {
        var services = await _serviceManagementService.ListAsync(categoryId);
        var response = services
            .Where(s => s.IsActive)
            .Select(s => new PartnerCatalogServiceResponse(s.Id, s.CategoryId, s.Name))
            .ToList();

        return Ok(response);
    }
}

/// <summary>Category name lookup entry - deliberately just id/name, unlike the admin-facing <see cref="CategoryResponse"/>.</summary>
public sealed record PartnerCatalogCategoryResponse(Guid Id, string Name);

/// <summary>Service name lookup entry - deliberately just id/categoryId/name, unlike the admin-facing <see cref="ServiceAdminResponse"/>.</summary>
public sealed record PartnerCatalogServiceResponse(Guid Id, Guid CategoryId, string Name);
