using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.AdminRoleManagement;
using Nestly.Domain;
using Nestly.Infrastructure.Auditing;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Identity.Tests;

/// <summary>
/// Role CRUD and permission-matrix editing (SRS 12.2.2, 12.2.3, task 313),
/// exercised end to end through <see cref="AdminRoleManagementService"/> and
/// the real repositories over a relational database - same rationale as
/// <see cref="AdminUserManagementServiceTests"/>: uniqueness, FK integrity
/// and audit rows all happen in SQL, so stubbed repositories would prove
/// nothing.
///
/// The self-escalation guard tests are this file's most important cases -
/// see <see cref="AdminRoleManagementService"/>'s doc comment for the exact
/// rule they pin.
/// </summary>
public class AdminRoleManagementServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    private AdminRoleManagementService CreateService(NestlyDbContext context) =>
        new(
            new AdminRoleRepository(context),
            new AdminUserRepository(context),
            new AdminRolePermissionQueryService(context),
            new AuditLogWriter(context, new StubAuditContextProvider()),
            context);

    private async Task<Guid> SeedRoleAsync(string name = "Test Role", string description = "")
    {
        await using var context = _database.CreateContext();
        var role = new AdminRole(Guid.NewGuid(), name, description);
        context.Add(role);
        await context.SaveChangesAsync();
        return role.Id;
    }

    /// <summary>Seeds an admin account whose token would carry exactly <paramref name="permissionCodes"/> - i.e. a role holding exactly that grant.</summary>
    private async Task<Guid> SeedAdminUserWithPermissionsAsync(IReadOnlyList<string> permissionCodes)
    {
        await using var context = _database.CreateContext();

        var role = new AdminRole(Guid.NewGuid(), $"Role-{Guid.NewGuid():N}", "Scoped role for a test admin");
        context.Add(role);

        foreach (string code in permissionCodes)
        {
            var definition = AdminPermissionCatalog.Permissions.Single(p => p.Code == code);
            var permission = new AdminPermission(Guid.NewGuid(), definition.Code, definition.Module, definition.Description);
            context.Add(permission);
            context.Add(new RolePermissionMapping(Guid.NewGuid(), role.Id, permission.Id));
        }

        var adminUser = new AdminUser(Guid.NewGuid(), $"{Guid.NewGuid():N}@example.com", "placeholder", "Test Admin");
        adminUser.SetPasswordHash("placeholder-hash");
        adminUser.AssignRole(role.Id);
        context.Add(adminUser);

        await context.SaveChangesAsync();
        return adminUser.Id;
    }

    [Fact]
    public async Task Creating_a_role_persists_it_and_is_audited()
    {
        Guid actingAdminUserId = await SeedAdminUserWithPermissionsAsync(["settings.write"]);

        await using var context = _database.CreateContext();
        var result = await CreateService(context).CreateAsync(
            new CreateAdminRoleRequest("Support Lead", "Support escalation", PermissionCodes: []),
            actingAdminUserId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Support Lead");

        var stored = await context.Set<AdminRole>().SingleAsync(r => r.Id == result.Value.Id);
        stored.Name.Should().Be("Support Lead");

        var auditEntry = await context.Set<AuditLog>()
            .SingleAsync(a => a.EntityName == "AdminRole" && a.EntityId == result.Value.Id.ToString() && a.Action == "AdminRoleCreated");
        auditEntry.Should().NotBeNull();
    }

    [Fact]
    public async Task Creating_a_role_with_a_duplicate_name_is_rejected()
    {
        await SeedRoleAsync("Taken Name");
        Guid actingAdminUserId = await SeedAdminUserWithPermissionsAsync(["settings.write"]);

        await using var context = _database.CreateContext();
        var result = await CreateService(context).CreateAsync(
            new CreateAdminRoleRequest("Taken Name", "", PermissionCodes: []), actingAdminUserId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AdminRole.NameInUse");
    }

    [Fact]
    public async Task Creating_a_role_with_an_unknown_permission_code_is_rejected()
    {
        Guid actingAdminUserId = await SeedAdminUserWithPermissionsAsync(["settings.write"]);

        await using var context = _database.CreateContext();
        var result = await CreateService(context).CreateAsync(
            new CreateAdminRoleRequest("New Role", "", PermissionCodes: ["not-a-real-code"]), actingAdminUserId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AdminPermission.UnknownCode");
    }

    [Fact]
    public async Task Renaming_a_role_does_not_touch_its_permission_grants()
    {
        Guid actingAdminUserId = await SeedAdminUserWithPermissionsAsync(["settings.write"]);

        Guid roleId;
        await using (var context = _database.CreateContext())
        {
            var created = await CreateService(context).CreateAsync(
                new CreateAdminRoleRequest("Original Name", "Original description", ["settings.write"]),
                actingAdminUserId);
            roleId = created.Value.Id;
        }

        await using var context2 = _database.CreateContext();
        var result = await CreateService(context2).UpdateAsync(
            roleId, new UpdateAdminRoleRequest("Renamed", "New description"), actingAdminUserId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Renamed");
        result.Value.PermissionCodes.Should().BeEquivalentTo(["settings.write"]);
    }

    [Fact]
    public async Task Renaming_a_role_to_a_name_already_in_use_is_rejected()
    {
        Guid roleId = await SeedRoleAsync("Role A");
        await SeedRoleAsync("Role B");
        Guid actingAdminUserId = await SeedAdminUserWithPermissionsAsync(["settings.write"]);

        await using var context = _database.CreateContext();
        var result = await CreateService(context).UpdateAsync(
            roleId, new UpdateAdminRoleRequest("Role B", "desc"), actingAdminUserId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AdminRole.NameInUse");
    }

    [Fact]
    public async Task Setting_a_roles_permissions_replaces_the_full_grant_and_is_audited()
    {
        Guid roleId = await SeedRoleAsync();
        Guid actingAdminUserId = await SeedAdminUserWithPermissionsAsync(["settings.write", "catalog.write"]);

        await using var context = _database.CreateContext();
        var result = await CreateService(context).SetPermissionsAsync(
            roleId, new SetAdminRolePermissionsRequest(["settings.write", "catalog.write"]), actingAdminUserId);

        result.IsSuccess.Should().BeTrue();
        result.Value.PermissionCodes.Should().BeEquivalentTo(["settings.write", "catalog.write"]);

        var auditEntry = await context.Set<AuditLog>()
            .SingleAsync(a => a.EntityName == "AdminRole" && a.EntityId == roleId.ToString() && a.Action == "AdminRolePermissionsUpdated");
        auditEntry.NewValues.Should().Contain("catalog.write");

        // Replacing again with a smaller set actually removes the dropped code.
        var second = await CreateService(context).SetPermissionsAsync(
            roleId, new SetAdminRolePermissionsRequest(["settings.write"]), actingAdminUserId);
        second.Value.PermissionCodes.Should().BeEquivalentTo(["settings.write"]);
    }

    [Fact]
    public async Task Setting_permissions_on_an_unknown_role_returns_not_found()
    {
        Guid actingAdminUserId = await SeedAdminUserWithPermissionsAsync(["settings.write"]);

        await using var context = _database.CreateContext();
        var result = await CreateService(context).SetPermissionsAsync(
            Guid.NewGuid(), new SetAdminRolePermissionsRequest(["settings.write"]), actingAdminUserId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AdminRole.NotFound");
    }

    /// <summary>
    /// The core security property (task 313): an admin holding only
    /// "settings.write" must not be able to grant a *different* role a
    /// permission set that includes a code the admin does not hold
    /// (here, "catalog.write"). This is falsifiable - see this class's doc
    /// remarks in the completion report for the mutation-test that confirms
    /// removing the guard makes this fail.
    /// </summary>
    [Fact]
    public async Task An_admin_cannot_grant_another_role_a_permission_they_do_not_hold()
    {
        Guid roleId = await SeedRoleAsync("Target Role");
        Guid actingAdminUserId = await SeedAdminUserWithPermissionsAsync(["settings.write"]);

        await using var context = _database.CreateContext();
        var result = await CreateService(context).SetPermissionsAsync(
            roleId,
            new SetAdminRolePermissionsRequest(["settings.write", "catalog.write"]),
            actingAdminUserId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AdminRole.SelfEscalationBlocked");

        // Rejected atomically - the role must not end up with even the
        // permission the acting admin *did* hold.
        var grant = await new AdminRolePermissionQueryService(context).GetPermissionsAsync(roleId);
        grant.PermissionCodes.Should().BeEmpty();
    }

    /// <summary>
    /// The second half of the security property: an admin cannot edit the
    /// permissions of the role currently assigned to their own account to
    /// add a permission they do not already hold - not even by attempting to
    /// grant it to "their own" role, where a naive per-role-identity check
    /// might have been tempted to special-case an exemption.
    /// </summary>
    [Fact]
    public async Task An_admin_cannot_add_a_permission_to_their_own_currently_assigned_role()
    {
        Guid bootstrapActorId = await SeedAdminUserWithPermissionsAsync(["settings.write"]);

        await using var context = _database.CreateContext();
        var service = CreateService(context);

        var createResult = await service.CreateAsync(
            new CreateAdminRoleRequest("Self Role", "", ["settings.write"]),
            actingAdminUserId: bootstrapActorId); // seeded by a bootstrap actor, not the test subject
        Guid ownRoleId = createResult.Value.Id;

        var adminUser = new AdminUser(Guid.NewGuid(), $"{Guid.NewGuid():N}@example.com", "placeholder", "Self Admin");
        adminUser.SetPasswordHash("placeholder-hash");
        adminUser.AssignRole(ownRoleId);
        context.Add(adminUser);
        await context.SaveChangesAsync();

        var result = await service.SetPermissionsAsync(
            ownRoleId,
            new SetAdminRolePermissionsRequest(["settings.write", "catalog.write"]),
            actingAdminUserId: adminUser.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AdminRole.SelfEscalationBlocked");

        var grant = await new AdminRolePermissionQueryService(context).GetPermissionsAsync(ownRoleId);
        grant.PermissionCodes.Should().BeEquivalentTo(["settings.write"]);
    }

    /// <summary>Removing a permission from your own role is not an escalation - only additions are blocked.</summary>
    [Fact]
    public async Task An_admin_can_reduce_their_own_roles_permissions()
    {
        Guid actingAdminUserId = await SeedAdminUserWithPermissionsAsync(["settings.write", "catalog.write"]);

        await using var context = _database.CreateContext();
        var adminUser = await context.Set<AdminUser>().SingleAsync(a => a.Id == actingAdminUserId);
        Guid ownRoleId = adminUser.RoleId!.Value;

        var result = await CreateService(context).SetPermissionsAsync(
            ownRoleId, new SetAdminRolePermissionsRequest(["settings.write"]), actingAdminUserId);

        result.IsSuccess.Should().BeTrue();
        result.Value.PermissionCodes.Should().BeEquivalentTo(["settings.write"]);
    }

    /// <summary>A Super-Admin-equivalent (holds every code) can grant every code - the guard never blocks a legitimate full grant.</summary>
    [Fact]
    public async Task An_admin_holding_every_permission_can_grant_any_role_any_permission()
    {
        var allCodes = AdminPermissionCatalog.Permissions.Select(p => p.Code).ToList();
        Guid actingAdminUserId = await SeedAdminUserWithPermissionsAsync(allCodes);
        Guid roleId = await SeedRoleAsync("Any Role");

        await using var context = _database.CreateContext();
        var result = await CreateService(context).SetPermissionsAsync(
            roleId, new SetAdminRolePermissionsRequest(["settings.write", "catalog.write", "bookings.write"]), actingAdminUserId);

        result.IsSuccess.Should().BeTrue();
        result.Value.PermissionCodes.Should().BeEquivalentTo(["settings.write", "catalog.write", "bookings.write"]);
    }

    [Fact]
    public async Task Listing_roles_returns_every_roles_permission_codes()
    {
        Guid roleId = await SeedRoleAsync("Listable Role");
        Guid actingAdminUserId = await SeedAdminUserWithPermissionsAsync(["settings.write"]);

        await using (var context = _database.CreateContext())
        {
            await CreateService(context).SetPermissionsAsync(
                roleId, new SetAdminRolePermissionsRequest(["settings.write"]), actingAdminUserId);
        }

        await using var listContext = _database.CreateContext();
        var result = await CreateService(listContext).ListAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(r => r.Id == roleId && r.PermissionCodes.Contains("settings.write"));
    }

    [Fact]
    public async Task Permission_catalog_returns_every_module_read_write_pair()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).GetPermissionCatalogAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(AdminPermissionCatalog.Permissions.Count);
        result.Value.Should().Contain(p => p.Code == "settings.write");
    }

    private sealed class StubAuditContextProvider : IAuditContextProvider
    {
        public AuditContext GetCurrent() =>
            new(AuditActorType.AdminUser, ActorId: Guid.NewGuid(), IpAddress: "127.0.0.1", CorrelationId: "test-correlation-id");
    }

    public void Dispose() => _database.Dispose();
}
