using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Seed;

namespace Nestly.Identity.Tests;

/// <summary>
/// The startup permission reconciliation (task 332, QA-REPORT-2026-08-18 bug
/// #5). Exercised over a real relational database rather than stubs because
/// what broke was data that was never written: the unique index on
/// <c>admin_permission.code</c> and the one on
/// <c>role_permission_mapping (role_id, permission_id)</c> are the guards
/// that make the "run it twice" case meaningful, and both live in SQL.
///
/// The scenario each test starts from is the one found in the live database:
/// every module seeded by a migration is present, and the module added
/// afterwards (Payments, task 311) has no rows at all.
/// </summary>
public class AdminPermissionReconcilerTests : IDisposable
{
    private const string LateAddedModule = AdminModules.Payments;

    private readonly TestDatabase _database = new();

    private static AdminPermissionReconciler CreateReconciler(NestlyDbContext context) =>
        new(context, NullLogger<AdminPermissionReconciler>.Instance);

    /// <summary>Seeds the nine default roles, as <c>AddAdminPermissionMatrix</c> does.</summary>
    private async Task SeedDefaultRolesAsync()
    {
        await using var context = _database.CreateContext();
        foreach (string roleName in AdminRoleNames.All)
        {
            context.Add(new AdminRole(Guid.NewGuid(), roleName, AdminRoleNames.Descriptions[roleName]));
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Reproduces the live database's state: every module except
    /// <see cref="LateAddedModule"/> already seeded, with the grants the
    /// catalog prescribes for it.
    /// </summary>
    private async Task SeedEveryModuleExceptTheLateAddedOneAsync()
    {
        await SeedDefaultRolesAsync();

        await using var context = _database.CreateContext();
        var roleIdsByName = await context.Set<AdminRole>().ToDictionaryAsync(role => role.Name, role => role.Id);

        foreach (var definition in AdminPermissionCatalog.Permissions.Where(p => p.Module != LateAddedModule))
        {
            var permission = new AdminPermission(
                Guid.NewGuid(), definition.Code, definition.Module, definition.Description);
            context.Add(permission);

            foreach (string roleName in AdminRoleNames.All)
            {
                if (AdminPermissionCatalog.RolePermissionCodes[roleName].Contains(definition.Code))
                {
                    context.Add(new RolePermissionMapping(Guid.NewGuid(), roleIdsByName[roleName], permission.Id));
                }
            }
        }

        await context.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<string>> GetPermissionCodesAsync()
    {
        await using var context = _database.CreateContext();
        return await context.Set<AdminPermission>().Select(permission => permission.Code).ToListAsync();
    }

    private async Task<IReadOnlyList<string>> GetGrantedCodesAsync(string roleName)
    {
        await using var context = _database.CreateContext();
        return await (
            from mapping in context.Set<RolePermissionMapping>()
            join permission in context.Set<AdminPermission>() on mapping.PermissionId equals permission.Id
            join role in context.Set<AdminRole>() on mapping.RoleId equals role.Id
            where role.Name == roleName
            select permission.Code).ToListAsync();
    }

    [Fact]
    public async Task Every_module_ends_up_with_a_read_and_a_write_permission_row()
    {
        await SeedEveryModuleExceptTheLateAddedOneAsync();

        await using (var context = _database.CreateContext())
        {
            await CreateReconciler(context).ReconcileAsync();
        }

        var storedCodes = await GetPermissionCodesAsync();

        storedCodes.Should().BeEquivalentTo(AdminPermissionCatalog.Permissions.Select(p => p.Code));
        foreach (string module in AdminModules.All)
        {
            storedCodes.Should().Contain(AdminPermissionCatalog.BuildCode(module, AdminPermissionAction.Read));
            storedCodes.Should().Contain(AdminPermissionCatalog.BuildCode(module, AdminPermissionAction.Write));
        }
    }

    [Fact]
    public async Task Super_admin_is_granted_every_permission_in_the_catalog()
    {
        await SeedEveryModuleExceptTheLateAddedOneAsync();

        await using (var context = _database.CreateContext())
        {
            await CreateReconciler(context).ReconcileAsync();
        }

        var grantedCodes = await GetGrantedCodesAsync(AdminRoleNames.SuperAdmin);

        grantedCodes.Should().BeEquivalentTo(AdminPermissionCatalog.Permissions.Select(p => p.Code));
    }

    [Fact]
    public async Task Reconciling_a_database_that_has_never_been_seeded_creates_the_whole_matrix()
    {
        await SeedDefaultRolesAsync();

        await using (var context = _database.CreateContext())
        {
            var result = await CreateReconciler(context).ReconcileAsync();
            result.CreatedPermissionCodes.Should().BeEquivalentTo(
                AdminPermissionCatalog.Permissions.Select(p => p.Code));
        }

        (await GetPermissionCodesAsync()).Should().BeEquivalentTo(
            AdminPermissionCatalog.Permissions.Select(p => p.Code));
        (await GetGrantedCodesAsync(AdminRoleNames.SuperAdmin)).Should().BeEquivalentTo(
            AdminPermissionCatalog.Permissions.Select(p => p.Code));
    }

    [Fact]
    public async Task Running_it_a_second_time_changes_nothing()
    {
        await SeedEveryModuleExceptTheLateAddedOneAsync();

        await using (var context = _database.CreateContext())
        {
            (await CreateReconciler(context).ReconcileAsync()).MadeChanges.Should().BeTrue();
        }

        var codesAfterFirstRun = await GetPermissionCodesAsync();
        var grantsAfterFirstRun = await GetGrantedCodesAsync(AdminRoleNames.SuperAdmin);

        await using (var context = _database.CreateContext())
        {
            var secondRun = await CreateReconciler(context).ReconcileAsync();

            secondRun.MadeChanges.Should().BeFalse();
            secondRun.CreatedPermissionCodes.Should().BeEmpty();
            secondRun.CreatedGrants.Should().BeEmpty();
        }

        (await GetPermissionCodesAsync()).Should().BeEquivalentTo(codesAfterFirstRun);
        (await GetGrantedCodesAsync(AdminRoleNames.SuperAdmin)).Should().BeEquivalentTo(grantsAfterFirstRun);
    }

    [Fact]
    public async Task A_permission_an_operator_revoked_from_super_admin_is_not_re_granted()
    {
        await SeedEveryModuleExceptTheLateAddedOneAsync();

        await using (var context = _database.CreateContext())
        {
            await CreateReconciler(context).ReconcileAsync();
        }

        string revokedCode = AdminPermissionCatalog.BuildCode(LateAddedModule, AdminPermissionAction.Write);

        // What the permission-matrix UI does: drop the grant row, leave the
        // admin_permission row in place.
        await using (var context = _database.CreateContext())
        {
            var revokedMapping = await (
                from mapping in context.Set<RolePermissionMapping>()
                join permission in context.Set<AdminPermission>() on mapping.PermissionId equals permission.Id
                join role in context.Set<AdminRole>() on mapping.RoleId equals role.Id
                where role.Name == AdminRoleNames.SuperAdmin && permission.Code == revokedCode
                select mapping).SingleAsync();

            context.Remove(revokedMapping);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            (await CreateReconciler(context).ReconcileAsync()).MadeChanges.Should().BeFalse();
        }

        var grantedCodes = await GetGrantedCodesAsync(AdminRoleNames.SuperAdmin);

        grantedCodes.Should().NotContain(revokedCode);
        grantedCodes.Should().Contain(AdminPermissionCatalog.BuildCode(LateAddedModule, AdminPermissionAction.Read));
    }

    [Fact]
    public async Task Grants_follow_the_catalog_for_every_default_role_not_just_super_admin()
    {
        await SeedEveryModuleExceptTheLateAddedOneAsync();

        await using (var context = _database.CreateContext())
        {
            await CreateReconciler(context).ReconcileAsync();
        }

        foreach (string roleName in AdminRoleNames.All)
        {
            (await GetGrantedCodesAsync(roleName)).Should().BeEquivalentTo(
                AdminPermissionCatalog.RolePermissionCodes[roleName],
                $"{roleName} should hold exactly the codes the catalog grants it");
        }
    }

    [Fact]
    public async Task Permissions_are_still_created_when_the_default_roles_are_missing()
    {
        await using (var context = _database.CreateContext())
        {
            var result = await CreateReconciler(context).ReconcileAsync();

            result.CreatedPermissionCodes.Should().HaveCount(AdminPermissionCatalog.Permissions.Count);
            result.CreatedGrants.Should().BeEmpty();
        }

        (await GetPermissionCodesAsync()).Should().BeEquivalentTo(
            AdminPermissionCatalog.Permissions.Select(p => p.Code));
    }

    public void Dispose() => _database.Dispose();
}
