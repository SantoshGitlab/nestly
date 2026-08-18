using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Amc;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin visibility into AMC contracts (docs/AMC.md): search/filter by
/// status and customer, contract detail, and the renewal-pipeline report -
/// mirrors the shape <see cref="RecurringPlansController"/> already
/// established for its own search-plus-report pair.
///
/// RBAC: gated behind the existing "bookings.read", with no new
/// <c>AdminModules</c> entry - docs/AMC.md's RBAC ADDITIONS section applies
/// the exact same reasoning <see cref="RecurringPlansController"/>'s doc
/// comment gives for recurring plans: an AMC contract is a way bookings come
/// into existence, and every row this controller reports on is either a
/// <see cref="Domain.CustomerAmcContract"/> or a <see cref="Booking"/>
/// carrying its id, both already readable in strictly more detail through
/// <c>BookingsController</c> to any admin holding "bookings.read". A new
/// permission gating a strictly weaker view of data already readable is an
/// inconvenience, not a boundary.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/amc-contracts")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class AmcContractsController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Bookings + ".read";

    private readonly IAmcAdminService _adminService;

    public AmcContractsController(IAmcAdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>Every AMC contract on the platform, newest first, filterable by status and searchable by customer name/mobile.</summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(AmcContractAdminSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] CustomerAmcContractStatus? status,
        [FromQuery] string? customerSearch,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _adminService.SearchContractsAsync(status, customerSearch, page, pageSize);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(AmcContractAdminListItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _adminService.GetContractByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Contract status counts plus contracts expiring or exhausted within a horizon (defaults to the next 30 days) - the "needs a renewal conversation" list docs/AMC.md's renewal pipeline exists for.</summary>
    [HttpGet("renewal-report")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(AmcRenewalReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRenewalReport([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        var result = await _adminService.GetRenewalReportAsync(fromUtc, toUtc);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }
}
