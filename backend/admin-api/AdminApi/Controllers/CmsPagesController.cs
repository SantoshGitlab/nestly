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
/// Admin static page management (SRS 12.16.1 "About / policy pages", "SEO
/// content for key public pages", tasks 124a/124c/124d/124f): CRUD plus
/// draft/publish workflow, optional publish window, and placement. Read-only
/// actions require "cms.read"; every mutating action requires "cms.write"
/// (task 96b/96c) - a role granted Write always also holds Read (see
/// <c>AdminPermissionCatalog</c>), so the two are applied per-action rather
/// than a single class-level policy, matching <c>CouponsController</c>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/cms/pages")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class CmsPagesController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Cms + ".read";
    private const string WritePolicy = AdminModules.Cms + ".write";

    private readonly ICmsPageService _pageService;
    private readonly IValidator<CmsPageAdminSearchRequest> _searchValidator;
    private readonly IValidator<CmsPageCreateRequest> _createValidator;
    private readonly IValidator<CmsPageUpdateRequest> _updateValidator;

    public CmsPagesController(
        ICmsPageService pageService,
        IValidator<CmsPageAdminSearchRequest> searchValidator,
        IValidator<CmsPageCreateRequest> createValidator,
        IValidator<CmsPageUpdateRequest> updateValidator)
    {
        _pageService = pageService;
        _searchValidator = searchValidator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Search/filter pages (SRS 12.16.1).</summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(CmsPageAdminSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] string? title,
        [FromQuery] string? slug,
        [FromQuery] CmsContentStatus? status,
        [FromQuery] CmsPlacement? placement,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var request = new CmsPageAdminSearchRequest(title, slug, status, placement, page, pageSize);

        var validation = await _searchValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        return Ok(await _pageService.SearchAsync(request));
    }

    /// <summary>Page detail (SRS 12.16.1).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(CmsPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _pageService.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Creates a page. Always starts as Draft - see <see cref="Publish"/>.</summary>
    [HttpPost]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(CmsPageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CmsPageCreateRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _pageService.CreateAsync(request);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : result.ToProblemResult();
    }

    /// <summary>Edits every mutable page field (SRS 12.16.1).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(CmsPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CmsPageUpdateRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _pageService.UpdateAsync(id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Publishes a draft page, or re-publishes one already live (SRS 12.16.2 "draft/publish status").</summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(Guid id)
    {
        var result = await _pageService.PublishAsync(id);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>Pulls a page back to draft without deleting it.</summary>
    [HttpPost("{id:guid}/unpublish")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        var result = await _pageService.UnpublishAsync(id);
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
