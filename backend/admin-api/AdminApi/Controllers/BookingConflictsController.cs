using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.ProviderManagement;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Task 321/322: standing provider double-bookings - the bookings one provider
/// is live on at overlapping times - and the count behind the dashboard badge.
///
/// <para>
/// Read-only by design. Resolution is a reassignment, and a reassignment
/// already has exactly one entry point:
/// <c>POST /admin/bookings/{bookingId}/assign-provider</c>. Adding a
/// "resolve conflict" mutation here would be a second write path to the same
/// state, with its own copy of the validation, the supersede rules and the
/// task 288 conflict check - the precise duplication
/// <see cref="IBookingProviderAssignmentService"/>'s doc comment exists to
/// prevent. The dashboard therefore reads from here and writes through
/// <c>BookingsController</c>, which also means every conflict resolution lands
/// in the audit trail as the ordinary assignment it is.
/// </para>
///
/// <para>
/// RBAC: the existing "bookings.read", no new <c>AdminModules</c> entry - the
/// same reasoning <c>RecurringPlansController</c> records at length. Every row
/// exposed here is a booking the caller can already open individually through
/// <c>BookingsController</c>, and in more detail; a new permission gating a
/// strictly weaker view of already-readable data is not a boundary.
/// </para>
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/booking-conflicts")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme)]
public class BookingConflictsController : ControllerBase
{
    private const string ReadPolicy = AdminModules.Bookings + ".read";

    private readonly IBookingAssignmentConflictService _conflictService;

    public BookingConflictsController(IBookingAssignmentConflictService conflictService)
    {
        _conflictService = conflictService;
    }

    /// <summary>
    /// Conflict groups, soonest first. Both dates are optional:
    /// <paramref name="fromDate"/> defaults to today, because a clash in the
    /// past can no longer be resolved by moving anyone and would only pad the
    /// list an admin is working through. Pass an explicit earlier
    /// <paramref name="fromDate"/> to audit historical damage.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(BookingAssignmentConflictSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Search(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _conflictService.SearchAsync(fromDate, toDate, page, pageSize, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }

    /// <summary>Number of outstanding conflict groups from <paramref name="fromDate"/> (default today) onward.</summary>
    [HttpGet("count")]
    [Authorize(Policy = ReadPolicy)]
    [ProducesResponseType(typeof(BookingConflictCountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Count([FromQuery] DateOnly? fromDate, CancellationToken cancellationToken = default)
    {
        var result = await _conflictService.CountAsync(fromDate, cancellationToken);
        return result.IsSuccess ? Ok(new BookingConflictCountResponse(result.Value)) : result.ToProblemResult();
    }
}

/// <summary>Wraps the badge count in an object rather than returning a bare int, so the shape can grow without breaking clients.</summary>
public sealed record BookingConflictCountResponse(int ConflictCount);
