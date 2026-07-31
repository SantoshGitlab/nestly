using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Infrastructure;

namespace Nestly.PartnerApi.Controllers;

/// <summary>
/// Partner earnings and payouts (task 149c, PARTNER.md API surface
/// "Earnings" — summary, ledger, payouts list/detail).
///
/// STUB: every action here returns 501 Not Implemented. PARTNER.md's
/// Financial Domain entities, <c>PartnerEarningLedger</c> and
/// <c>PartnerPayout</c>, are being built concurrently on a sibling branch
/// (task 148) and are not yet present in this worktree. Route shapes below
/// match the API surface PARTNER.md describes so wiring this up is a
/// drop-in once 148 merges: swap each body for a call into an
/// <c>IPartnerEarningsService</c> (to be introduced then) backed by
/// <c>IPartnerEarningLedgerRepository</c>/<c>IPartnerPayoutRepository</c>,
/// following the same "resolve the caller's own partner id from the JWT"
/// pattern as <see cref="ProfileController"/>.
///
/// TODO(task 148): replace every action body once PartnerEarningLedger/PartnerPayout land.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Authorize(AuthenticationSchemes = DependencyInjection.PartnerJwtBearerScheme)]
[Route("api/v{version:apiVersion}/earnings")]
public class EarningsController : ControllerBase
{
    /// <summary>Rolled-up earnings summary (total earned, pending payout, etc.) for the caller.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult GetSummary() => NotImplementedPendingFinancialDomain();

    /// <summary>Append-only earnings ledger entries for the caller.</summary>
    [HttpGet("ledger")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult GetLedger() => NotImplementedPendingFinancialDomain();

    /// <summary>Payout batches for the caller.</summary>
    [HttpGet("payouts")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult ListPayouts() => NotImplementedPendingFinancialDomain();

    /// <summary>One payout's detail.</summary>
    [HttpGet("payouts/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult GetPayoutDetail(Guid id) => NotImplementedPendingFinancialDomain();

    private ObjectResult NotImplementedPendingFinancialDomain() =>
        StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
        {
            Status = StatusCodes.Status501NotImplemented,
            Title = "Earnings.NotYetAvailable",
            Detail = "Earnings and payouts are not available yet - they depend on the " +
                     "PartnerEarningLedger/PartnerPayout entities (task 148), which have not merged into this API yet."
        });
}
