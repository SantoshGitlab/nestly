using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Seed;

/// <summary>One grant this reconciliation pass created: role name and the permission code it was given.</summary>
public sealed record AdminPermissionGrant(string RoleName, string PermissionCode);

/// <summary>What a single <see cref="AdminPermissionReconciler.ReconcileAsync"/> pass changed.</summary>
/// <param name="CreatedPermissionCodes">Catalog codes that had no <c>admin_permission</c> row until this pass.</param>
/// <param name="CreatedGrants">The <c>role_permission_mapping</c> rows seeded for those newly created permissions.</param>
public sealed record AdminPermissionReconciliationResult(
    IReadOnlyList<string> CreatedPermissionCodes,
    IReadOnlyList<AdminPermissionGrant> CreatedGrants)
{
    /// <summary>Nothing was missing - the pass wrote no rows at all.</summary>
    public static AdminPermissionReconciliationResult NoChanges { get; } = new([], []);

    public bool MadeChanges => CreatedPermissionCodes.Count > 0 || CreatedGrants.Count > 0;
}

/// <summary>
/// Task 332 (QA-REPORT-2026-08-18 bug #5): makes sure every module in
/// <see cref="AdminModules"/> actually has its <c>admin_permission</c> rows,
/// and that the default roles hold them per <see cref="AdminPermissionCatalog"/>,
/// on both a freshly migrated database and one that predates a module.
///
/// <para>
/// <b>Why a reconciler rather than one more seed migration.</b> Every module
/// added since task 96a shipped its own incremental <c>SeedXPermissions</c>
/// migration (Payout, Referral, Chat, Subscription, NestlyCoins) - Payments
/// (task 311) did not, so <c>GET /api/v1/admin/payments</c> 403'd even for
/// Super Admin, whose whole definition is "full access to every module".
/// A sixth hand-written seed migration would fix exactly that module and
/// leave the seventh module to be forgotten the same way, because a
/// migration <em>must</em> freeze its module list (see
/// <c>AddAdminPermissionMatrix.OriginalModules</c>: reading the live catalog
/// from inside a migration re-seeds later modules and breaks
/// <c>database update</c> from an empty database). Only something that runs
/// against the current catalog at runtime can stay correct as
/// <see cref="AdminModules.All"/> grows, which is why this runs at startup
/// (<see cref="AdminPermissionReconciliationExtensions.ReconcileAdminPermissions"/>)
/// instead.
/// </para>
///
/// <para>
/// <b>Deliberate revocations are never undone.</b> The reconciler seeds
/// grants <em>only</em> for permissions whose <c>admin_permission</c> row it
/// creates in that same pass. The existence of that row is the marker that
/// the module has already been seeded once, so from then on the grant matrix
/// belongs entirely to the operator: if a Super Admin revokes, say,
/// <c>payments.write</c> from the Super Admin role through the permission
/// matrix UI, the next restart finds the permission row present, seeds
/// nothing, and the revocation stands. The reconciler only ever fills in a
/// module nobody has made a decision about yet.
/// </para>
///
/// <para>
/// Idempotent by construction: a pass whose catalog is fully present issues
/// no writes at all (not even a <c>SaveChanges</c>). Ids are deterministic on
/// the same scheme the seed migrations use, so a row created here is
/// identical to the one the missing seed migration would have inserted.
/// </para>
/// </summary>
public sealed class AdminPermissionReconciler
{
    private readonly NestlyDbContext _dbContext;
    private readonly ILogger<AdminPermissionReconciler> _logger;

    public AdminPermissionReconciler(NestlyDbContext dbContext, ILogger<AdminPermissionReconciler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Adds the <c>admin_permission</c> rows the catalog defines but the
    /// database lacks, plus the default-role grants for exactly those rows.
    /// Safe to call on every startup.
    /// </summary>
    public async Task<AdminPermissionReconciliationResult> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var existingCodes = (await _dbContext.Set<AdminPermission>()
            .Select(permission => permission.Code)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var missingDefinitions = AdminPermissionCatalog.Permissions
            .Where(definition => !existingCodes.Contains(definition.Code))
            .ToList();

        if (missingDefinitions.Count == 0)
        {
            _logger.LogDebug(
                "Admin permission matrix is complete: all {PermissionCount} catalog permissions are present.",
                AdminPermissionCatalog.Permissions.Count);
            return AdminPermissionReconciliationResult.NoChanges;
        }

        var createdPermissionsByCode = missingDefinitions.ToDictionary(
            definition => definition.Code,
            definition => new AdminPermission(
                PermissionId(definition.Code), definition.Code, definition.Module, definition.Description));

        _dbContext.AddRange(createdPermissionsByCode.Values);

        var createdGrants = await SeedGrantsForAsync(createdPermissionsByCode, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded {PermissionCount} missing admin permission(s) ({PermissionCodes}) and {GrantCount} default-role grant(s).",
            createdPermissionsByCode.Count,
            string.Join(", ", createdPermissionsByCode.Keys.Order()),
            createdGrants.Count);

        return new AdminPermissionReconciliationResult(
            createdPermissionsByCode.Keys.Order().ToList(),
            createdGrants);
    }

    /// <summary>
    /// Grants each default role the subset of <paramref name="createdPermissionsByCode"/>
    /// the catalog says it holds. Roles absent from the database are skipped
    /// rather than created: a deployment whose <c>admin_role</c> rows are
    /// missing has a migration problem this reconciler should surface, not
    /// paper over by inventing roles nobody configured.
    /// </summary>
    private async Task<IReadOnlyList<AdminPermissionGrant>> SeedGrantsForAsync(
        IReadOnlyDictionary<string, AdminPermission> createdPermissionsByCode,
        CancellationToken cancellationToken)
    {
        var roleIdsByName = await _dbContext.Set<AdminRole>()
            .ToDictionaryAsync(role => role.Name, role => role.Id, cancellationToken);

        var createdGrants = new List<AdminPermissionGrant>();
        var absentRoleNames = new List<string>();

        foreach (string roleName in AdminRoleNames.All)
        {
            if (!roleIdsByName.TryGetValue(roleName, out Guid roleId))
            {
                absentRoleNames.Add(roleName);
                continue;
            }

            foreach (string code in AdminPermissionCatalog.RolePermissionCodes[roleName].Order())
            {
                if (!createdPermissionsByCode.TryGetValue(code, out var permission))
                {
                    continue;
                }

                _dbContext.Add(new RolePermissionMapping(GrantId(roleName, code), roleId, permission.Id));
                createdGrants.Add(new AdminPermissionGrant(roleName, code));
            }
        }

        if (absentRoleNames.Count > 0)
        {
            _logger.LogWarning(
                "Default admin role(s) {RoleNames} are missing from the database - their permission grants were not seeded.",
                string.Join(", ", absentRoleNames));
        }

        return createdGrants;
    }

    // Same scheme as AddAdminPermissionMatrix.DeterministicId and every
    // SeedXPermissions migration since: reference data, not events, so the
    // rows this writes match what a seed migration for the same module would
    // have written byte for byte.
    private static Guid DeterministicId(string seed) => new(MD5.HashData(Encoding.UTF8.GetBytes(seed)));

    private static Guid PermissionId(string code) => DeterministicId($"admin_permission:{code}");

    private static Guid GrantId(string roleName, string code) =>
        DeterministicId($"role_permission_mapping:{roleName}:{code}");
}
