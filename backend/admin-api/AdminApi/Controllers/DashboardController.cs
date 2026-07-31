using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Dashboard;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Admin dashboard KPI widgets (SRS 12.3, task 99): bookings, revenue,
/// cancellations, refunds, and open support tickets, filterable by date
/// range/city/category (SRS 12.3.2).
///
/// Gated behind "dashboard.read" (task 96b) - this is a read-only view, so it
/// only ever needs the module's Read tier, never Write.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/dashboard")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = DashboardReadPolicy)]
public class DashboardController : ControllerBase
{
    private const string DashboardReadPolicy = AdminModules.Dashboard + ".read";

    private readonly IDashboardQueryService _dashboardQueryService;

    public DashboardController(IDashboardQueryService dashboardQueryService)
    {
        _dashboardQueryService = dashboardQueryService;
    }

    /// <summary>Computes the KPI widgets for the given filters (all optional - an unset date range defaults to today).</summary>
    [HttpGet("kpis")]
    [ProducesResponseType(typeof(DashboardKpiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetKpis(
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] string? city,
        [FromQuery] string? category)
    {
        var result = await _dashboardQueryService.GetKpisAsync(new DashboardFilterRequest(dateFrom, dateTo, city, category));
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }
}
