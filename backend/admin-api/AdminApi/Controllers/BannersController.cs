using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Cms;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin banner management (SRS 12.16.1 "Home banners / Category banners /
/// Promotional blocks", tasks 124b/124c/124d/124f): CRUD plus draft/publish
/// workflow, media asset reference, category-scoped placement, ordering, and
/// an optional publish window. Read-only actions require "cms.read"; every
/// mutating action requires "cms.write" (task 96b/96c), matching
/// <c>CouponsController</c>'s per-action policy split.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/cms/banners")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class BannersController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Cms + ".read";
    private const string WritePolicy = AdminModules.Cms + ".write";

    private readonly IBannerService _bannerService;
    private readonly IValidator<BannerAdminSearchRequest> _searchValidator;
    private readonly IValidator<BannerCreateRequest> _createValidator;
    private readonly IValidator<BannerUpdateRequest> _updateValidator;

    public BannersController(
        IBannerService bannerService,
        IValidator<BannerAdminSearchRequest> searchValidator,
        IValidator<BannerCreateRequest> createValidator,
        IValidator<BannerUpdateRequest> updateValidator)
    {
        _bannerService = bannerService;
        _searchValidator = searchValidator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Active categories, for the banner form's "category" picker when placement is CategoryPage.</summary>
    [HttpGet("categories")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<Nestly.Application.Serviceability.CategoryLookupResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCategories() => Ok(await _bannerService.ListCategoriesAsync());

    /// <summary>The media library, for the banner form's asset picker (task 124e).</summary>
    [HttpGet("media")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<CmsMediaResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMedia() => Ok(await _bannerService.ListMediaAsync());

    /// <summary>Search/filter banners (SRS 12.16.1).</summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(BannerAdminSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] CmsPlacement? placement,
        [FromQuery] CmsContentStatus? status,
        [FromQuery] Guid? categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var request = new BannerAdminSearchRequest(placement, status, categoryId, page, pageSize);

        var validation = await _searchValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        return Ok(await _bannerService.SearchAsync(request));
    }

    /// <summary>Banner detail (SRS 12.16.1).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(BannerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _bannerService.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Creates a banner. Always starts as Draft - see <see cref="Publish"/>.</summary>
    [HttpPost]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(BannerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] BannerCreateRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _bannerService.CreateAsync(request);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : result.ToProblemResult();
    }

    /// <summary>Edits every mutable banner field (SRS 12.16.1).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(BannerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] BannerUpdateRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _bannerService.UpdateAsync(id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Publishes a draft banner, or re-publishes one already live (SRS 12.16.2 "draft/publish status").</summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(Guid id)
    {
        var result = await _bannerService.PublishAsync(id);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>Pulls a banner back to draft without deleting it.</summary>
    [HttpPost("{id:guid}/unpublish")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        var result = await _bannerService.UnpublishAsync(id);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    private static ModelStateDictionary ToModelState(ValidationResult validation)
    {
        var modelState = new ModelStateDictionary();
        foreach (var error in validation.Errors)
        {
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return modelState;
    }
}
