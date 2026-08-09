using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Payments;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin payment transaction view (SRS 12.13.1, task 311): a filterable
/// transaction list and a per-transaction detail (attempts + refunds), the
/// reconciliation surface admins previously only got incidentally through a
/// booking's own detail page (<c>BookingsController.GetDetail</c>'s embedded
/// <c>AdminBookingPaymentSummary</c>/<c>Refunds</c>). Read-only - see
/// <see cref="AdminModules.Payments"/>'s doc comment for why there is no
/// write endpoint here; refund initiation (SRS 12.13.2-3) remains
/// <c>BookingsController</c>'s "bookings.write"-gated action.
///
/// A transaction id that does not exist 404s rather than 403ing (SRS 28.3
/// IDOR guard, same rule <c>docs/API.md</c>'s address-endpoint section
/// documents) - there is no ownership concept to hide behind here since
/// every admin holding "payments.read" may see every transaction, but the
/// convention of never leaking existence via status code is kept consistent
/// with the rest of the admin API regardless.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/payments")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class PaymentsController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Payments + ".read";

    private readonly IAdminPaymentQueryService _paymentQueryService;

    public PaymentsController(IAdminPaymentQueryService paymentQueryService)
    {
        _paymentQueryService = paymentQueryService;
    }

    /// <summary>Transaction list, filterable by booking id, status and creation date range (SRS 12.13.1).</summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(PagedAdminPaymentTransactionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] AdminPaymentTransactionFilterRequest request)
    {
        var result = await _paymentQueryService.SearchAsync(request);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Full transaction detail: attempts and refunds (SRS 12.13.1, 14.3).</summary>
    [HttpGet("{transactionId:guid}")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(AdminPaymentTransactionDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid transactionId)
    {
        var result = await _paymentQueryService.GetDetailAsync(transactionId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }
}
