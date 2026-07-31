using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestly.Application.Auditing;
using Nestly.BuildingBlocks.Extensions;
using Nestly.Domain;
using Nestly.Infrastructure;

namespace Nestly.AdminApi.Controllers;

/// <summary>
/// Audit log viewer API (task 130, SRS 21): a filterable read over the
/// existing audit trail written by <c>AdminLoginService</c> (task 95g's
/// login audit) and <c>PermissionAuthorizationHandler</c> (task 96d's
/// permission-check audit) — no second audit table is introduced here.
///
/// Gated behind "audit.read", the permission code
/// <see cref="AdminPermissionCatalog"/> defines for <see cref="AdminModules.Audit"/>.
/// Per the seeded role matrix (task 96a), only Super Admin holds it today -
/// even Finance Admin, which lists the audit module in its notes, is granted
/// Read only through that same catalog, matching what is enforced here.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin/audit-log")]
[Authorize(AuthenticationSchemes = DependencyInjection.AdminJwtBearerScheme, Policy = AuditReadPolicy)]
public class AuditLogController : ControllerBase
{
    private const string AuditReadPolicy = AdminModules.Audit + ".read";

    private readonly IAuditLogQueryService _auditLogQueryService;

    public AuditLogController(IAuditLogQueryService auditLogQueryService)
    {
        _auditLogQueryService = auditLogQueryService;
    }

    /// <summary>
    /// Searches the audit trail (SRS 21.1-21.2), filterable by actor, date
    /// range, entity/action, and outcome (task 130). Newest first, paginated.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedAuditLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Search([FromQuery] AuditLogFilterRequest request, CancellationToken cancellationToken)
    {
        var result = await _auditLogQueryService.SearchAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }
}
