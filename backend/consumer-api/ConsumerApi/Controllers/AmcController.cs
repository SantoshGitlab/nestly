using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Amc;
using Nestly.Application.Bookings;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>
/// Customer-facing AMC flow (docs/AMC.md): browse the plan catalog, purchase
/// a contract for a named asset, view "my AMC contracts", cancel, and redeem
/// entitlement into an ordinary booking. Every action scoped to the caller's
/// own customer id, same pattern as <see cref="SubscriptionController"/>.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
public class AmcController : ControllerBase
{
    private readonly IAmcCustomerService _amcService;
    private readonly IValidator<AmcContractPurchaseRequest> _purchaseValidator;
    private readonly IValidator<BookingSummaryRequest> _redeemValidator;

    public AmcController(
        IAmcCustomerService amcService,
        IValidator<AmcContractPurchaseRequest> purchaseValidator,
        IValidator<BookingSummaryRequest> redeemValidator)
    {
        _amcService = amcService;
        _purchaseValidator = purchaseValidator;
        _redeemValidator = redeemValidator;
    }

    /// <summary>Every AMC plan currently open to new purchases, optionally filtered to one service category.</summary>
    [HttpGet("api/v{version:apiVersion}/amc/plans")]
    [ProducesResponseType(typeof(IReadOnlyList<AmcPlanBrowseResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BrowsePlans([FromQuery] Guid? categoryId) =>
        Ok(await _amcService.BrowsePlansAsync(categoryId));

    /// <summary>Purchases a plan for a named asset (docs/AMC.md - records the contract; does not charge a real payment, see OPEN DECISIONS #4).</summary>
    [HttpPost("api/v{version:apiVersion}/amc/contracts")]
    [ProducesResponseType(typeof(MyAmcContractResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Purchase([FromBody] AmcContractPurchaseRequest request)
    {
        var validation = await _purchaseValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _amcService.PurchaseAsync(CurrentCustomerId(), request);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetMyContract), new { id = result.Value.Id }, result.Value)
            : result.ToProblemResult();
    }

    /// <summary>The caller's AMC contracts, active and past, newest first.</summary>
    [HttpGet("api/v{version:apiVersion}/me/amc-contracts")]
    [ProducesResponseType(typeof(IReadOnlyList<MyAmcContractResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMyContracts()
    {
        var result = await _amcService.ListMyContractsAsync(CurrentCustomerId());
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>One contract's detail plus its full visit history.</summary>
    [HttpGet("api/v{version:apiVersion}/me/amc-contracts/{id:guid}")]
    [ProducesResponseType(typeof(MyAmcContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyContract(Guid id)
    {
        var result = await _amcService.GetMyContractAsync(CurrentCustomerId(), id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Customer-initiated cancellation - immediate, terminal.</summary>
    [HttpPost("api/v{version:apiVersion}/me/amc-contracts/{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var result = await _amcService.CancelAsync(CurrentCustomerId(), id);
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
    }

    /// <summary>
    /// Redeems entitlement against a contract: creates a zero-priced booking
    /// through the same orchestration a normal "Book now" tap uses (docs/AMC.md).
    /// Entitlement itself is drawn down only once the resulting booking
    /// reaches Completed, not here.
    /// </summary>
    [HttpPost("api/v{version:apiVersion}/me/amc-contracts/{id:guid}/redeem")]
    [ProducesResponseType(typeof(BookingDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Redeem(Guid id, [FromBody] BookingSummaryRequest request)
    {
        var validation = await _redeemValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _amcService.RedeemVisitAsync(CurrentCustomerId(), id, request);
        return result.IsSuccess
            ? CreatedAtAction(nameof(BookingsController.Detail), "Bookings", new { id = result.Value.Id }, result.Value)
            : result.ToProblemResult();
    }

    private Guid CurrentCustomerId() =>
        User.GetSubjectId();

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
