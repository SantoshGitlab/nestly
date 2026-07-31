using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Identity;
using Nestly.Domain;
using Nestly.Infrastructure.Auditing;
using Nestly.Infrastructure.Authorization;

namespace Nestly.Identity.Tests;

/// <summary>
/// Phase 6 closing QA suite (task 132a): walks every (role, permission code)
/// combination in the matrix through the real <see cref="PermissionAuthorizationHandler"/>
/// - not just the single hardcoded permission/role pair
/// <see cref="PermissionAuthorizationHandlerTests"/> exercises, and not just
/// the happy path.
///
/// Catalog-level invariants (every module has exactly one read/write pair,
/// codes are unique, write implies read, Super Admin holds everything,
/// Read-only Analyst reads everything and writes nothing) are already
/// covered by <see cref="AdminPermissionCatalogTests"/> and are deliberately
/// not duplicated here. This file's job is confirming the *runtime
/// enforcement* actually matches that static matrix for every one of the
/// <see cref="AdminRoleNames.All"/> x <see cref="AdminPermissionCatalog.Permissions"/>
/// combinations (9 roles x 30 codes = 270 checks), and that every
/// combination the matrix does not grant is both denied by the handler and
/// written to the audit trail - the explicit "denial, not just the happy
/// path" requirement task 132a calls out.
/// </summary>
public class AdminRbacQaSuiteTests
{
    public static IEnumerable<object[]> AllRoles() =>
        AdminRoleNames.All.Select(role => new object[] { role });

    private sealed class StubAuditContextProvider(Guid actorId) : IAuditContextProvider
    {
        public AuditContext GetCurrent() =>
            new(AuditActorType.AdminUser, actorId, IpAddress: "127.0.0.1", CorrelationId: "test-correlation-id");
    }

    private static ClaimsPrincipal BuildUser(Guid actorId, IEnumerable<string> grantedCodes)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, actorId.ToString()) };
        claims.AddRange(grantedCodes.Select(code => new Claim(AdminClaimTypes.Permission, code)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    /// <summary>
    /// For a given role, evaluates every permission code in the catalog
    /// against the real handler: codes the role's static grant holds must
    /// succeed, every other code must be denied AND leave a
    /// "PermissionDenied:{code}" row in the audit trail attributed to the
    /// acting admin user - the handler is deliberately not
    /// <c>context.Fail()</c>'d (see its own doc comment), so the only way to
    /// tell a wrongly-permissive grant from a correct denial is checking
    /// both <see cref="AuthorizationHandlerContext.HasSucceeded"/> and the
    /// audit row together, which is exactly what this test does for every
    /// combination rather than a hand-picked one.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllRoles))]
    public async Task Every_permission_code_is_granted_or_denied_exactly_as_the_static_matrix_says(string role)
    {
        using var database = new TestDatabase();
        var actorId = Guid.NewGuid();
        IReadOnlySet<string> grantedCodes = AdminPermissionCatalog.RolePermissionCodes[role];
        var user = BuildUser(actorId, grantedCodes);

        foreach (AdminPermissionDefinition permission in AdminPermissionCatalog.Permissions)
        {
            await using var context = database.CreateContext();
            var handler = new PermissionAuthorizationHandler(
                new AuditLogWriter(context, new StubAuditContextProvider(actorId)), context);
            var authContext = new AuthorizationHandlerContext(
                [new PermissionRequirement(permission.Code)], user, resource: null);

            await handler.HandleAsync(authContext);

            bool shouldBeGranted = grantedCodes.Contains(permission.Code);
            authContext.HasSucceeded.Should().Be(
                shouldBeGranted,
                $"role '{role}' {(shouldBeGranted ? "should" : "should not")} hold '{permission.Code}' per AdminPermissionCatalog.RolePermissionCodes");

            if (!shouldBeGranted)
            {
                var deniedEntry = await context.Set<AuditLog>().SingleOrDefaultAsync(a =>
                    a.EntityName == "AdminPermissionCheck" &&
                    a.EntityId == actorId.ToString() &&
                    a.Action == $"PermissionDenied:{permission.Code}");

                deniedEntry.Should().NotBeNull(
                    $"a denied check for '{permission.Code}' under role '{role}' must be audited, not silently dropped");
                deniedEntry!.ActorId.Should().Be(actorId);
            }
        }
    }

    /// <summary>
    /// A concrete, human-readable instance of the matrix above: Booking
    /// Admin's module list (see <c>AdminPermissionCatalog.BuildRoleModuleGrants</c>)
    /// never mentions <c>catalog</c> at all, so this role must be denied
    /// both <c>catalog.read</c> and <c>catalog.write</c> outright - kept as
    /// its own fact (in addition to the exhaustive theory above) because a
    /// role missing a module entirely, rather than merely lacking write on a
    /// module it can read, is a distinct failure mode worth naming directly.
    /// </summary>
    [Fact]
    public async Task Booking_admin_is_denied_a_module_it_was_never_granted_at_all()
    {
        using var database = new TestDatabase();
        var actorId = Guid.NewGuid();
        IReadOnlySet<string> grantedCodes = AdminPermissionCatalog.RolePermissionCodes[AdminRoleNames.BookingAdmin];
        grantedCodes.Should().NotContain("catalog.read").And.NotContain("catalog.write");

        var user = BuildUser(actorId, grantedCodes);

        await using var context = database.CreateContext();
        var handler = new PermissionAuthorizationHandler(
            new AuditLogWriter(context, new StubAuditContextProvider(actorId)), context);
        var authContext = new AuthorizationHandlerContext(
            [new PermissionRequirement("catalog.write")], user, resource: null);

        await handler.HandleAsync(authContext);

        authContext.HasSucceeded.Should().BeFalse();

        var deniedEntry = await context.Set<AuditLog>().SingleAsync(a =>
            a.EntityName == "AdminPermissionCheck" && a.EntityId == actorId.ToString());
        deniedEntry.Action.Should().Be("PermissionDenied:catalog.write");
        deniedEntry.ActorType.Should().Be(AuditActorType.AdminUser);
    }
}
