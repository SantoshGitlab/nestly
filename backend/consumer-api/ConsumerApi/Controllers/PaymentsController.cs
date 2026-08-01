using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly IPaymentWebhookService _webhookService;
    private readonly IValidator<CreatePaymentOrderRequest> _createOrderValidator;
    private readonly IValidator<PaymentWebhookRequest> _webhookValidator;
    private readonly IValidator<SimulatePaymentRequest> _simulateValidator;

    public PaymentsController(
        IPaymentService paymentService,
        IPaymentWebhookService webhookService,
        IValidator<CreatePaymentOrderRequest> createOrderValidator,
        IValidator<PaymentWebhookRequest> webhookValidator,
        IValidator<SimulatePaymentRequest> simulateValidator)
    {
        _paymentService = paymentService;
        _webhookService = webhookService;
        _createOrderValidator = createOrderValidator;
        _webhookValidator = webhookValidator;
        _simulateValidator = simulateValidator;
    }

    /// <summary>
    /// Creates a gateway order for a booking's payment, or (task 70) retries
    /// after a prior failure - the same endpoint serves both, since a retry
    /// is just "create an order for a booking whose last attempt failed".
    /// Idempotent for a booking already awaiting a callback (task 68d).
    /// </summary>
    [HttpPost("orders")]
    [Authorize]
    [EnableRateLimiting("payment")]
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

    /// <summary>
    /// The gateway's payment callback (SRS 30.1, 11.11.3, tasks 69a-c).
    /// Deliberately not [Authorize] - the caller is the payment gateway, not
    /// a logged-in customer, and is authenticated by its signature instead
    /// (SRS 28.3 "payment callback abuse"). Always idempotent (task 69b): a
    /// redelivered callback for an already-resolved attempt is a no-op 200,
    /// never re-applied.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [EnableRateLimiting("payment-webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Webhook([FromBody] PaymentWebhookRequest request)
    {
        var validation = await _webhookValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _webhookService.HandleCallbackAsync(request);
        return result.IsSuccess ? Ok() : result.ToProblemResult();
    }

    /// <summary>
    /// Sandbox-only convenience (task 68b): simulates a gateway completing
    /// payment for an order the caller owns, deterministically per
    /// <c>SandboxPaymentGateway</c>'s amount convention, by constructing and
    /// signing the same callback <see cref="Webhook"/> handles for real.
    /// There is no equivalent endpoint for a real gateway integration - only
    /// the gateway itself can decide a payment's outcome.
    /// </summary>
    [HttpPost("orders/simulate")]
    [Authorize]
    [EnableRateLimiting("payment")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Simulate([FromBody] SimulatePaymentRequest request)
    {
        var validation = await _simulateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(ToModelState(validation));
        }

        var result = await _paymentService.SimulateAsync(CurrentCustomerId(), request);
        // NoContent, not Ok(): Ok() with no value still serialises a 200
        // with an empty body, which frontend/customer-web's apiFetch (only
        // special-cased for 204) then fails to JSON-parse - task 140b's E2E
        // suite caught this as a real "Failed to execute 'json' on
        // 'Response'" crash on the sandbox payment button, not a test-only
        // issue. NoContent matches every other no-body success response in
        // this codebase (see CategoriesController.Activate for the pattern).
        return result.IsSuccess ? NoContent() : result.ToProblemResult();
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
