using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.AdminRoleManagement;
using Nestly.Application.Identity;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Role CRUD and permission-matrix editing (SRS 12.2.2, 12.2.3, task 313) over
/// <see cref="AdminRole"/> and <see cref="RolePermissionMapping"/>. Writes an
/// audit entry for every mutation, same convention as
/// <see cref="AdminUserManagementService"/> - editing who can do what is at
/// least as security-sensitive as editing one account.
///
/// <para>
/// <b>Self-escalation guard</b> (<see cref="CheckForSelfEscalationAsync"/>):
/// whenever a write would cause a role to hold a permission code it did not
/// already hold, every one of those newly-granted codes must already be held
/// by the acting admin (resolved fresh from their own current role via
/// <see cref="IAdminRolePermissionQueryService"/>, not from JWT claims that
/// may have gone stale since login). This single rule covers both halves of
/// the security requirement without special-casing "is this my own role?":
/// </para>
/// <list type="bullet">
/// <item>Granting a different role a permission the acting admin does not
/// hold is rejected outright (nothing in the request is a no-op removal).</item>
/// <item>Editing the role currently assigned to the acting admin's own
/// account can only ever <em>remove</em> permissions relative to what that
/// role (and therefore the acting admin) already grants - anything "new" to
/// that role is by definition not yet held by the acting admin, so it is
/// always rejected. An admin cannot use this endpoint to promote their own
/// account.</item>
/// </list>
/// <para>
/// Missing <see cref="AdminPermission"/> rows for a valid catalog code are
/// lazily created from <see cref="AdminPermissionCatalog"/> the first time
/// they are referenced (<see cref="ResolvePermissionsAsync"/>), which keeps
/// this endpoint working even against a database whose seed data predates a
/// module. It is no longer the only thing that populates
/// <c>admin_permission</c>, though: relying on it alone is exactly what left
/// Super Admin without the Payments module until an operator opened this UI
/// (task 332), so <c>AdminPermissionReconciler</c> now seeds every catalog
/// permission at admin-api startup instead.
/// </para>
/// </summary>
public class AdminRoleManagementService : IAdminRoleManagementService
{
    private readonly IAdminRoleRepository _adminRoleRepository;
    private readonly IAdminUserRepository _adminUserRepository;
    private readonly IAdminRolePermissionQueryService _rolePermissionQueryService;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly NestlyDbContext _dbContext;

    public AdminRoleManagementService(
        IAdminRoleRepository adminRoleRepository,
        IAdminUserRepository adminUserRepository,
        IAdminRolePermissionQueryService rolePermissionQueryService,
        IAuditLogWriter auditLogWriter,
        NestlyDbContext dbContext)
    {
        _adminRoleRepository = adminRoleRepository;
        _adminUserRepository = adminUserRepository;
        _rolePermissionQueryService = rolePermissionQueryService;
        _auditLogWriter = auditLogWriter;
        _dbContext = dbContext;
    }

    public Task<Result<IReadOnlyList<AdminPermissionCatalogEntryResponse>>> GetPermissionCatalogAsync()
    {
        IReadOnlyList<AdminPermissionCatalogEntryResponse> catalog = AdminPermissionCatalog.Permissions
            .Select(p => new AdminPermissionCatalogEntryResponse(p.Code, p.Module, p.Action, p.Description))
            .ToList();

        return Task.FromResult(Result.Success(catalog));
    }

    public async Task<Result<IReadOnlyList<AdminRoleDetailResponse>>> ListAsync()
    {
        var roles = await _adminRoleRepository.ListAllAsync();

        // One join across every role rather than one GetPermissionsAsync call
        // per row - the role list is small (tens, not thousands) but there is
        // no reason to pay an N+1 for it.
        var grants = await (
            from mapping in _dbContext.Set<RolePermissionMapping>().AsNoTracking()
            join permission in _dbContext.Set<AdminPermission>().AsNoTracking()
                on mapping.PermissionId equals permission.Id
            select new { mapping.RoleId, permission.Code }).ToListAsync();

        var codesByRole = grants
            .GroupBy(g => g.RoleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Code).OrderBy(c => c).ToList());

        IReadOnlyList<AdminRoleDetailResponse> response = roles
            .Select(role => ToDetail(role, codesByRole.TryGetValue(role.Id, out var codes) ? codes : Array.Empty<string>()))
            .ToList();

        return Result.Success(response);
    }

    public async Task<Result<AdminRoleDetailResponse>> GetByIdAsync(Guid roleId)
    {
        var role = await _adminRoleRepository.GetByIdAsync(roleId);
        if (role is null)
        {
            return NotFound();
        }

        var grant = await _rolePermissionQueryService.GetPermissionsAsync(roleId);
        return ToDetail(role, grant.PermissionCodes);
    }

    public async Task<Result<AdminRoleDetailResponse>> CreateAsync(CreateAdminRoleRequest request, Guid actingAdminUserId)
    {
        if (await _adminRoleRepository.GetByNameAsync(request.Name) is not null)
        {
            return Error.Conflict("AdminRole.NameInUse", "A role with that name already exists.");
        }

        var resolved = await ResolvePermissionsAsync(request.PermissionCodes);
        if (resolved.IsFailure)
        {
            return resolved.Error;
        }

        Error? escalation = await CheckForSelfEscalationAsync(
            actingAdminUserId, currentCodes: Array.Empty<string>(), targetCodes: resolved.Value.Select(p => p.Code));
        if (escalation is not null)
        {
            return escalation;
        }

        var role = new AdminRole(Guid.NewGuid(), request.Name, request.Description);
        await _adminRoleRepository.AddAsync(role);
        await ReplaceRoleGrantsAsync(role.Id, resolved.Value);
        await WriteAuditAsync(role.Id, "AdminRoleCreated", actingAdminUserId);

        return ToDetail(role, resolved.Value.Select(p => p.Code).OrderBy(c => c).ToList());
    }

    public async Task<Result<AdminRoleDetailResponse>> UpdateAsync(Guid roleId, UpdateAdminRoleRequest request, Guid actingAdminUserId)
    {
        var role = await _adminRoleRepository.GetByIdAsync(roleId);
        if (role is null)
        {
            return NotFound();
        }

        var existingWithName = await _adminRoleRepository.GetByNameAsync(request.Name);
        if (existingWithName is not null && existingWithName.Id != roleId)
        {
            return Error.Conflict("AdminRole.NameInUse", "A role with that name already exists.");
        }

        role.Rename(request.Name);
        role.SetDescription(request.Description);
        await _adminRoleRepository.UpdateAsync(role);
        await WriteAuditAsync(role.Id, "AdminRoleUpdated", actingAdminUserId);

        var grant = await _rolePermissionQueryService.GetPermissionsAsync(roleId);
        return ToDetail(role, grant.PermissionCodes);
    }

    public async Task<Result<AdminRoleDetailResponse>> SetPermissionsAsync(Guid roleId, SetAdminRolePermissionsRequest request, Guid actingAdminUserId)
    {
        var role = await _adminRoleRepository.GetByIdAsync(roleId);
        if (role is null)
        {
            return NotFound();
        }

        var resolved = await ResolvePermissionsAsync(request.PermissionCodes);
        if (resolved.IsFailure)
        {
            return resolved.Error;
        }

        var currentGrant = await _rolePermissionQueryService.GetPermissionsAsync(roleId);

        Error? escalation = await CheckForSelfEscalationAsync(
            actingAdminUserId, currentGrant.PermissionCodes, resolved.Value.Select(p => p.Code));
        if (escalation is not null)
        {
            return escalation;
        }

        await ReplaceRoleGrantsAsync(roleId, resolved.Value);

        var oldCodes = string.Join(",", currentGrant.PermissionCodes.OrderBy(c => c));
        var newCodes = string.Join(",", resolved.Value.Select(p => p.Code).OrderBy(c => c));
        await WriteAuditAsync(role.Id, "AdminRolePermissionsUpdated", actingAdminUserId, oldCodes, newCodes);

        return ToDetail(role, resolved.Value.Select(p => p.Code).OrderBy(c => c).ToList());
    }

    /// <summary>
    /// Rejects a permission write the moment it would grant the target role
    /// any code the acting admin does not currently hold themselves - see
    /// this class's doc comment for why one rule covers both the
    /// "escalate someone else's role" and "escalate my own role" cases.
    /// </summary>
    private async Task<Error?> CheckForSelfEscalationAsync(
        Guid actingAdminUserId, IEnumerable<string> currentCodes, IEnumerable<string> targetCodes)
    {
        var newlyGrantedCodes = targetCodes.Except(currentCodes).ToList();
        if (newlyGrantedCodes.Count == 0)
        {
            return null;
        }

        var actingAdmin = await _adminUserRepository.GetByIdAsync(actingAdminUserId);
        var actingGrant = await _rolePermissionQueryService.GetPermissionsAsync(actingAdmin?.RoleId);
        var actingPermissionCodes = actingGrant.PermissionCodes.ToHashSet();

        var escalatingCodes = newlyGrantedCodes.Where(c => !actingPermissionCodes.Contains(c)).ToList();
        if (escalatingCodes.Count == 0)
        {
            return null;
        }

        return Error.Forbidden(
            "AdminRole.SelfEscalationBlocked",
            $"You cannot grant permission(s) you do not already hold: {string.Join(", ", escalatingCodes.OrderBy(c => c))}.");
    }

    /// <summary>
    /// Validates every requested code against the catalog and returns the
    /// backing <see cref="AdminPermission"/> rows, creating any that are
    /// missing from <c>admin_permission</c> (see this class's doc comment).
    /// </summary>
    private async Task<Result<List<AdminPermission>>> ResolvePermissionsAsync(IReadOnlyList<string> codes)
    {
        var distinctCodes = codes.Distinct().ToList();
        var catalogByCode = AdminPermissionCatalog.Permissions.ToDictionary(p => p.Code);

        var unknownCodes = distinctCodes.Where(c => !catalogByCode.ContainsKey(c)).ToList();
        if (unknownCodes.Count > 0)
        {
            return Error.Validation(
                "AdminPermission.UnknownCode",
                $"Unknown permission code(s): {string.Join(", ", unknownCodes)}.");
        }

        var existing = await _dbContext.Set<AdminPermission>()
            .Where(p => distinctCodes.Contains(p.Code))
            .ToListAsync();

        var missingCodes = distinctCodes.Except(existing.Select(p => p.Code)).ToList();
        foreach (string code in missingCodes)
        {
            var definition = catalogByCode[code];
            var permission = new AdminPermission(Guid.NewGuid(), definition.Code, definition.Module, definition.Description);
            _dbContext.Add(permission);
            existing.Add(permission);
        }

        if (missingCodes.Count > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        return existing;
    }

    private async Task ReplaceRoleGrantsAsync(Guid roleId, IReadOnlyList<AdminPermission> targetPermissions)
    {
        var existingMappings = await _dbContext.Set<RolePermissionMapping>()
            .Where(m => m.RoleId == roleId)
            .ToListAsync();
        _dbContext.RemoveRange(existingMappings);

        foreach (var permission in targetPermissions)
        {
            _dbContext.Add(new RolePermissionMapping(Guid.NewGuid(), roleId, permission.Id));
        }

        await _dbContext.SaveChangesAsync();
    }

    private static AdminRoleDetailResponse ToDetail(AdminRole role, IReadOnlyList<string> permissionCodes) =>
        new(role.Id, role.Name, role.Description, permissionCodes, role.CreatedAt, role.UpdatedAt);

    private async Task WriteAuditAsync(Guid roleId, string action, Guid actingAdminUserId, string? oldValues = null, string? newValues = null)
    {
        await _auditLogWriter.WriteAsync(new AuditEntry("AdminRole", roleId.ToString(), action, oldValues, newValues));
        await _dbContext.SaveChangesAsync();
    }

    private static Error NotFound() =>
        Error.NotFound("AdminRole.NotFound", "No role was found with that id.");
}
