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
/// Runtime permission enforcement (task 96b/96c) and its audit trail (task
/// 96d, SECURITY.md's "Permission failures" / "Administrative actions"
/// logging requirement) — exercised against a real database like
/// <see cref="AdminLoginServiceTests"/>, since what's under test is whether
/// an audit row actually lands, not just whether a method was called.
/// </summary>
public class PermissionAuthorizationHandlerTests : IDisposable
{
    private const string PermissionCode = "catalog.write";
    private static readonly Guid ActorId = Guid.NewGuid();

    private readonly TestDatabase _database = new();

    private sealed class StubAuditContextProvider : IAuditContextProvider
    {
        public AuditContext GetCurrent() =>
            new(AuditActorType.AdminUser, ActorId, IpAddress: "127.0.0.1", CorrelationId: "test-correlation-id");
    }

    private async Task<AuthorizationHandlerContext> EvaluateAsync(ClaimsPrincipal user)
    {
        await using var context = _database.CreateContext();
        var handler = new PermissionAuthorizationHandler(
            new AuditLogWriter(context, new StubAuditContextProvider()), context);

        var authContext = new AuthorizationHandlerContext(
            [new PermissionRequirement(PermissionCode)], user, resource: null);

        await handler.HandleAsync(authContext);
        return authContext;
    }

    private static ClaimsPrincipal UserWithClaims(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    [Fact]
    public async Task A_user_holding_the_permission_claim_is_granted()
    {
        var user = UserWithClaims(
            new Claim(ClaimTypes.NameIdentifier, ActorId.ToString()),
            new Claim(AdminClaimTypes.Permission, PermissionCode));

        var result = await EvaluateAsync(user);

        result.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task A_user_missing_the_permission_claim_is_denied()
    {
        var user = UserWithClaims(new Claim(ClaimTypes.NameIdentifier, ActorId.ToString()));

        var result = await EvaluateAsync(user);

        result.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task A_denial_is_written_to_the_audit_trail()
    {
        var user = UserWithClaims(new Claim(ClaimTypes.NameIdentifier, ActorId.ToString()));

        await EvaluateAsync(user);

        await using var context = _database.CreateContext();
        var entry = await context.Set<AuditLog>()
            .SingleAsync(a => a.EntityName == "AdminPermissionCheck" && a.EntityId == ActorId.ToString());

        entry.Action.Should().Be($"PermissionDenied:{PermissionCode}");
    }

    [Fact]
    public async Task A_write_permission_grant_is_audited_as_a_sensitive_action()
    {
        var user = UserWithClaims(
            new Claim(ClaimTypes.NameIdentifier, ActorId.ToString()),
            new Claim(AdminClaimTypes.Permission, PermissionCode));

        await EvaluateAsync(user);

        await using var context = _database.CreateContext();
        var entry = await context.Set<AuditLog>()
            .SingleAsync(a => a.EntityName == "AdminPermissionCheck" && a.EntityId == ActorId.ToString());

        entry.Action.Should().Be($"PermissionGranted:{PermissionCode}");
    }

    [Fact]
    public async Task A_read_permission_grant_is_not_audited()
    {
        const string readCode = "catalog.read";
        var user = UserWithClaims(
            new Claim(ClaimTypes.NameIdentifier, ActorId.ToString()),
            new Claim(AdminClaimTypes.Permission, readCode));

        await using var context = _database.CreateContext();
        var handler = new PermissionAuthorizationHandler(
            new AuditLogWriter(context, new StubAuditContextProvider()), context);
        var authContext = new AuthorizationHandlerContext([new PermissionRequirement(readCode)], user, resource: null);

        await handler.HandleAsync(authContext);

        authContext.HasSucceeded.Should().BeTrue();
        (await context.Set<AuditLog>().AnyAsync(a => a.EntityName == "AdminPermissionCheck")).Should().BeFalse();
    }

    public void Dispose() => _database.Dispose();
}
