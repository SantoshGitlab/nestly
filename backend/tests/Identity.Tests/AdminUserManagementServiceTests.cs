using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.AdminUserManagement;
using Nestly.Domain;
using Nestly.Infrastructure.Auditing;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Identity.Tests;

/// <summary>
/// Admin user management: CRUD, role assignment, activate/deactivate and
/// admin-initiated password reset (SRS 12.2.1, tasks 97a-97d), exercised end
/// to end through <see cref="AdminUserManagementService"/> and the real
/// repositories over a relational database - same rationale as
/// <see cref="AdminLoginServiceTests"/>: uniqueness and audit rows both
/// happen in SQL, so stubbed repositories would prove nothing.
/// </summary>
public class AdminUserManagementServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly Guid _actingAdminUserId = Guid.NewGuid();

    private AdminUserManagementService CreateService(NestlyDbContext context) =>
        new(
            new AdminUserRepository(context),
            new AdminRoleRepository(context),
            new AuditLogWriter(context, new StubAuditContextProvider()),
            context);

    private async Task<Guid> SeedRoleAsync()
    {
        await using var context = _database.CreateContext();
        var role = new AdminRole(Guid.NewGuid(), AdminRoleNames.SupportAdmin, "Support");
        context.Add(role);
        await context.SaveChangesAsync();
        return role.Id;
    }

    private async Task<Guid> SeedAdminUserAsync(string email = "admin@example.com", Guid? roleId = null)
    {
        await using var context = _database.CreateContext();
        var adminUser = new AdminUser(Guid.NewGuid(), email, "placeholder", "Test Admin");
        adminUser.SetPasswordHash(new PasswordHasher<AdminUser>().HashPassword(adminUser, "Correct-Horse-1!"));
        if (roleId.HasValue)
        {
            adminUser.AssignRole(roleId.Value);
        }

        context.Add(adminUser);
        await context.SaveChangesAsync();
        return adminUser.Id;
    }

    [Fact]
    public async Task Creating_an_admin_user_hashes_the_password_and_returns_its_detail()
    {
        await using var context = _database.CreateContext();
        var service = CreateService(context);

        var result = await service.CreateAsync(
            new CreateAdminUserRequest("new-admin@example.com", "New Admin", "Correct-Horse-1!", RoleId: null),
            _actingAdminUserId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("new-admin@example.com");
        result.Value.Status.Should().Be(AdminUserStatus.Active);

        var stored = await context.Set<AdminUser>().SingleAsync(a => a.Id == result.Value.Id);
        stored.PasswordHash.Should().NotBe("Correct-Horse-1!");
    }

    [Fact]
    public async Task Creating_an_admin_user_with_a_duplicate_email_is_rejected()
    {
        await SeedAdminUserAsync("taken@example.com");

        await using var context = _database.CreateContext();
        var result = await CreateService(context).CreateAsync(
            new CreateAdminUserRequest("taken@example.com", "Someone Else", "Correct-Horse-1!", RoleId: null),
            _actingAdminUserId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AdminUser.EmailInUse");
    }

    [Fact]
    public async Task Creating_an_admin_user_with_an_unknown_role_is_rejected()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).CreateAsync(
            new CreateAdminUserRequest("new-admin@example.com", "New Admin", "Correct-Horse-1!", Guid.NewGuid()),
            _actingAdminUserId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AdminRole.NotFound");
    }

    [Fact]
    public async Task Assigning_a_role_updates_the_account_and_is_audited()
    {
        Guid roleId = await SeedRoleAsync();
        Guid adminUserId = await SeedAdminUserAsync();

        await using var context = _database.CreateContext();
        var result = await CreateService(context).AssignRoleAsync(
            adminUserId, new AssignAdminRoleRequest(roleId), _actingAdminUserId);

        result.IsSuccess.Should().BeTrue();
        result.Value.RoleId.Should().Be(roleId);
        result.Value.RoleName.Should().Be(AdminRoleNames.SupportAdmin);

        var auditEntry = await context.Set<AuditLog>()
            .SingleAsync(a => a.EntityName == "AdminUser" && a.EntityId == adminUserId.ToString());
        auditEntry.Action.Should().Be("AdminUserRoleAssigned");
    }

    [Fact]
    public async Task Assigning_an_unknown_role_is_rejected()
    {
        Guid adminUserId = await SeedAdminUserAsync();

        await using var context = _database.CreateContext();
        var result = await CreateService(context).AssignRoleAsync(
            adminUserId, new AssignAdminRoleRequest(Guid.NewGuid()), _actingAdminUserId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AdminRole.NotFound");
    }

    [Fact]
    public async Task Clearing_a_role_assignment_is_allowed()
    {
        Guid roleId = await SeedRoleAsync();
        Guid adminUserId = await SeedAdminUserAsync(roleId: roleId);

        await using var context = _database.CreateContext();
        var result = await CreateService(context).AssignRoleAsync(
            adminUserId, new AssignAdminRoleRequest(RoleId: null), _actingAdminUserId);

        result.IsSuccess.Should().BeTrue();
        result.Value.RoleId.Should().BeNull();
        result.Value.RoleName.Should().BeNull();
    }

    [Fact]
    public async Task Deactivating_then_activating_an_admin_account_round_trips_the_status()
    {
        Guid adminUserId = await SeedAdminUserAsync();
        Guid otherActingAdmin = Guid.NewGuid();

        await using (var context = _database.CreateContext())
        {
            var deactivated = await CreateService(context).DeactivateAsync(adminUserId, otherActingAdmin);
            deactivated.IsSuccess.Should().BeTrue();
            deactivated.Value.Status.Should().Be(AdminUserStatus.Inactive);
        }

        await using (var context = _database.CreateContext())
        {
            var activated = await CreateService(context).ActivateAsync(adminUserId, otherActingAdmin);
            activated.IsSuccess.Should().BeTrue();
            activated.Value.Status.Should().Be(AdminUserStatus.Active);
        }
    }

    [Fact]
    public async Task An_admin_cannot_deactivate_their_own_account()
    {
        Guid adminUserId = await SeedAdminUserAsync();

        await using var context = _database.CreateContext();
        var result = await CreateService(context).DeactivateAsync(adminUserId, adminUserId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AdminUser.CannotDeactivateSelf");
    }

    [Fact]
    public async Task Resetting_a_password_generates_a_temporary_password_that_verifies_against_the_stored_hash()
    {
        Guid adminUserId = await SeedAdminUserAsync();

        await using var context = _database.CreateContext();
        var result = await CreateService(context).ResetPasswordAsync(adminUserId, _actingAdminUserId);

        result.IsSuccess.Should().BeTrue();
        result.Value.TemporaryPassword.Should().NotBeNullOrWhiteSpace();

        var stored = await context.Set<AdminUser>().SingleAsync(a => a.Id == adminUserId);
        var verification = new PasswordHasher<AdminUser>()
            .VerifyHashedPassword(stored, stored.PasswordHash, result.Value.TemporaryPassword);
        verification.Should().Be(PasswordVerificationResult.Success);
    }

    [Fact]
    public async Task Resetting_a_password_does_not_leak_it_into_the_audit_trail()
    {
        Guid adminUserId = await SeedAdminUserAsync();

        await using var context = _database.CreateContext();
        var result = await CreateService(context).ResetPasswordAsync(adminUserId, _actingAdminUserId);

        var auditEntry = await context.Set<AuditLog>()
            .SingleAsync(a => a.EntityName == "AdminUser" && a.EntityId == adminUserId.ToString());

        auditEntry.Action.Should().Be("AdminUserPasswordReset");
        auditEntry.OldValues.Should().NotContain(result.Value.TemporaryPassword);
        auditEntry.NewValues.Should().NotContain(result.Value.TemporaryPassword);
    }

    [Fact]
    public async Task Updating_the_profile_with_an_email_already_used_by_another_account_is_rejected()
    {
        await SeedAdminUserAsync("first@example.com");
        Guid secondAdminUserId = await SeedAdminUserAsync("second@example.com");

        await using var context = _database.CreateContext();
        var result = await CreateService(context).UpdateAsync(
            secondAdminUserId, new UpdateAdminUserRequest("first@example.com", "Second Admin"), _actingAdminUserId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AdminUser.EmailInUse");
    }

    [Fact]
    public async Task Searching_paginates_and_filters_by_status()
    {
        Guid activeId = await SeedAdminUserAsync("active@example.com");
        Guid inactiveId = await SeedAdminUserAsync("inactive@example.com");

        await using (var context = _database.CreateContext())
        {
            var toDeactivate = await context.Set<AdminUser>().SingleAsync(a => a.Id == inactiveId);
            toDeactivate.Deactivate();
            await context.SaveChangesAsync();
        }

        await using var searchContext = _database.CreateContext();
        var result = await CreateService(searchContext).SearchAsync(
            new AdminUserSearchRequest(Email: null, Name: null, AdminUserStatus.Active, RoleId: null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.Id == activeId);
        result.Value.Items.Should().NotContain(i => i.Id == inactiveId);
    }

    [Fact]
    public async Task Operating_on_an_unknown_admin_user_id_returns_not_found()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).GetByIdAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AdminUser.NotFound");
    }

    private sealed class StubAuditContextProvider : IAuditContextProvider
    {
        public AuditContext GetCurrent() =>
            new(AuditActorType.AdminUser, ActorId: Guid.NewGuid(), IpAddress: "127.0.0.1", CorrelationId: "test-correlation-id");
    }

    public void Dispose() => _database.Dispose();
}
