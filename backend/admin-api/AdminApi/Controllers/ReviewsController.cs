using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Reviews;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin review moderation (SRS 12.15, task 122): filterable search (status,
/// flagged, rating range, date, service/category), hide/unhide, flag/unflag,
/// and CSV export. Read-only actions require "reviews.read"; every mutating
/// action requires "reviews.write" (task 96b/96c) - same per-action split as
/// <see cref="CustomersController"/>, since a role granted Write always also
/// holds Read (see <c>AdminPermissionCatalog</c>).
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/reviews")]
[Authorize(AuthenticationSchemes = Nestly.Infrastructure.DependencyInjection.AdminJwtBearerScheme)]
public class ReviewsController : ControllerBase
{
    private const string ReviewsReadPolicy = AdminModules.Reviews + ".read";
    private const string ReviewsWritePolicy = AdminModules.Reviews + ".write";

    private readonly IReviewModerationService _reviewModerationService;
    private readonly IValidator<ReviewModerationSearchRequest> _searchValidator;
    private readonly IValidator<ModerateReviewRequest> _moderateValidator;

    public ReviewsController(
        IReviewModerationService reviewModerationService,
        IValidator<ReviewModerationSearchRequest> searchValidator,
        IValidator<ModerateReviewRequest> moderateValidator)
    {
        _reviewModerationService = reviewModerationService;
        _searchValidator = searchValidator;
        _moderateValidator = moderateValidator;
    }

    /// <summary>Filtered/paginated review search (SRS 12.15 "View reviews by service, category, date, rating").</summary>
    [HttpGet]
    [Authorize(Policy = ReviewsReadPolicy)]
    [ProducesResponseType(typeof(ReviewModerationSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] ReviewModerationSearchRequest request, CancellationToken cancellationToken)
    {
        var validation = await _searchValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _reviewModerationService.SearchAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>CSV export of every review matching the filter (SRS 12.15 "Export reviews").</summary>
    [HttpGet("export")]
    [Authorize(Policy = ReviewsReadPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export([FromQuery] ReviewModerationSearchRequest request, CancellationToken cancellationToken)
    {
        var validation = await _searchValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _reviewModerationService.ExportCsvAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        string fileName = $"reviews-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(result.Value, "text/csv", fileName);
    }

    /// <summary>Hides a review from the public/customer-facing side (SRS 12.15 "Hide/unhide reviews"). The original rating/text/tags are retained, never mutated.</summary>
    [HttpPost("{reviewId:guid}/hide")]
    [Authorize(Policy = ReviewsWritePolicy)]
    [ProducesResponseType(typeof(ReviewModerationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Hide(Guid reviewId, [FromBody] ModerateReviewRequest? request, CancellationToken cancellationToken) =>
        ModerateAsync(reviewId, request, cancellationToken, _reviewModerationService.HideAsync);

    /// <summary>Restores a hidden review to public visibility (SRS 12.15 "Hide/unhide reviews").</summary>
    [HttpPost("{reviewId:guid}/unhide")]
    [Authorize(Policy = ReviewsWritePolicy)]
    [ProducesResponseType(typeof(ReviewModerationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Unhide(Guid reviewId, [FromBody] ModerateReviewRequest? request, CancellationToken cancellationToken) =>
        ModerateAsync(reviewId, request, cancellationToken, _reviewModerationService.UnhideAsync);

    /// <summary>Flags a review as abusive/inappropriate content (SRS 12.15 "Flag abusive content"), independent of its hide/unhide visibility.</summary>
    [HttpPost("{reviewId:guid}/flag")]
    [Authorize(Policy = ReviewsWritePolicy)]
    [ProducesResponseType(typeof(ReviewModerationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Flag(Guid reviewId, [FromBody] ModerateReviewRequest? request, CancellationToken cancellationToken) =>
        ModerateAsync(reviewId, request, cancellationToken, _reviewModerationService.FlagAsync);

    /// <summary>Clears a review's abuse flag.</summary>
    [HttpPost("{reviewId:guid}/unflag")]
    [Authorize(Policy = ReviewsWritePolicy)]
    [ProducesResponseType(typeof(ReviewModerationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Unflag(Guid reviewId, [FromBody] ModerateReviewRequest? request, CancellationToken cancellationToken) =>
        ModerateAsync(reviewId, request, cancellationToken, _reviewModerationService.UnflagAsync);

    private async Task<IActionResult> ModerateAsync(
        Guid reviewId,
        ModerateReviewRequest? request,
        CancellationToken cancellationToken,
        Func<Guid, Guid, ModerateReviewRequest, CancellationToken, Task<Nestly.BuildingBlocks.Results.Result<ReviewModerationResponse>>> action)
    {
        var effectiveRequest = request ?? new ModerateReviewRequest(null);

        var validation = await _moderateValidator.ValidateAsync(effectiveRequest, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await action(reviewId, CurrentAdminUserId(), effectiveRequest, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    private Guid CurrentAdminUserId() =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

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
