using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Refunds;
using Nestly.BuildingBlocks.Extensions;

namespace Nestly.ConsumerApi.Controllers;

/// <summary>
/// Refund status per booking (SRS 11.17.2, task 78c). Read-only: a customer
/// can see refund status but cannot self-initiate a refund - that happens
/// through the cancellation flow (Phase 5) or an admin action (Phase 6),
/// both of which will call IRefundService directly once they exist.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize]
[Route("api/v{version:apiVersion}/bookings/{bookingId:guid}/refunds")]
public class RefundsController : ControllerBase
{
    private readonly IRefundService _refundService;

    public RefundsController(IRefundService refundService)
    {
        _refundService = refundService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RefundTransactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid bookingId)
    {
        var result = await _refundService.ListByBookingAsync(CurrentCustomerId(), bookingId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    private Guid CurrentCustomerId() =>
        User.GetSubjectId();
}
