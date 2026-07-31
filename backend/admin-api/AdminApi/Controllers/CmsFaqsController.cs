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
/// Admin site-level FAQ management (SRS 12.16.1 "FAQ entries", tasks
/// 124c/124d/124f): CRUD plus draft/publish workflow, sort order, placement,
/// and an optional publish window. Distinct from per-service FAQ management
/// (task 40e's <see cref="Nestly.Domain.ServiceFaq"/>, exposed via
/// <c>ServicesController</c>) - see <see cref="Nestly.Domain.CmsFaq"/>'s doc
/// comment. Read-only actions require "cms.read"; every mutating action
/// requires "cms.write" (task 96b/96c), matching <c>CouponsController</c>'s
/// per-action policy split.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/cms/faqs")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class CmsFaqsController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Cms + ".read";
    private const string WritePolicy = AdminModules.Cms + ".write";

    private readonly ICmsFaqService _faqService;
    private readonly IValidator<CmsFaqAdminSearchRequest> _searchValidator;
    private readonly IValidator<CmsFaqCreateRequest> _createValidator;
    private readonly IValidator<CmsFaqUpdateRequest> _updateValidator;

    public CmsFaqsController(
        ICmsFaqService faqService,
        IValidator<CmsFaqAdminSearchRequest> searchValidator,
        IValidator<CmsFaqCreateRequest> createValidator,
        IValidator<CmsFaqUpdateRequest> updateValidator)
    {
        _faqService = faqService;
        _searchValidator = searchValidator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Search/filter FAQ entries (SRS 12.16.1).</summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(CmsFaqAdminSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] CmsPlacement? placement,
        [FromQuery] CmsContentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var request = new CmsFaqAdminSearchRequest(placement, status, page, pageSize);

        var validation = await _searchValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        return Ok(await _faqService.SearchAsync(request));
    }

    /// <summary>FAQ detail (SRS 12.16.1).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(CmsFaqResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _faqService.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Creates a FAQ entry. Always starts as Draft - see <see cref="Publish"/>.</summary>
    [HttpPost]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(CmsFaqResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CmsFaqCreateRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _faqService.CreateAsync(request);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : result.ToProblemResult();
    }

    /// <summary>Edits every mutable FAQ field (SRS 12.16.1).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(typeof(CmsFaqResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CmsFaqUpdateRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _faqService.UpdateAsync(id, request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Publishes a draft FAQ entry, or re-publishes one already live (SRS 12.16.2 "draft/publish status").</summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(Guid id)
    {
        var result = await _faqService.PublishAsync(id);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>Pulls a FAQ entry back to draft without deleting it.</summary>
    [HttpPost("{id:guid}/unpublish")]
    [Authorize(Policy = WritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        var result = await _faqService.UnpublishAsync(id);
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
