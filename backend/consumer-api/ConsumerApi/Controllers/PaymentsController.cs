using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Application.Payments;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>Payments (SRS 11.11, 30.1). Order creation/retry is scoped to the caller's own customer id; the webhook is not (see its own doc comment).</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IValidator<CreatePaymentOrderRequest> _createOrderValidator;

    public PaymentsController(IPaymentService paymentService, IValidator<CreatePaymentOrderRequest> createOrderValidator)
    {
        _paymentService = paymentService;
        _createOrderValidator = createOrderValidator;
    }

    /// <summary>
    /// Creates a gateway order for a booking's payment, or (task 70) retries
    /// after a prior failure - the same endpoint serves both, since a retry
    /// is just "create an order for a booking whose last attempt failed".
    /// Idempotent for a booking already awaiting a callback (task 68d).
    /// </summary>
    [HttpPost("orders")]
    [Authorize]
    [ProducesResponseType(typeof(PaymentOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateOrder([FromBody] CreatePaymentOrderRequest request)
    {
        var validation = await _createOrderValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _paymentService.CreateOrderAsync(CurrentCustomerId(), request);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.ToProblemResult();
    }

    /// <summary>Payment transaction + attempt history for a booking (SRS 11.11.3, 14.3, task 71).</summary>
    [HttpGet("bookings/{bookingId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(PaymentTransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByBooking(Guid bookingId)
    {
        var result = await _paymentService.GetByBookingIdAsync(CurrentCustomerId(), bookingId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    private Guid CurrentCustomerId() =>
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
