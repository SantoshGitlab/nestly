using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Domain;

namespace Nestly.Infrastructure.Auditing;

/// <summary>
/// Resolves audit attribution from the current HTTP request (T020).
/// </summary>
/// <remarks>
/// Falls back to <see cref="AuditContext.System"/> when there is no ambient
/// request — which is what background jobs and console tooling get, and is the
/// correct attribution for them rather than a misleading "anonymous user".
/// </remarks>
public sealed class HttpAuditContextProvider : IAuditContextProvider
{
    /// <summary>
    /// Role name granting back-office access. Kept in one place so the audit
    /// attribution and the Hangfire dashboard filter cannot drift apart.
    /// </summary>
    public const string AdminRole = "Admin";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpAuditContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public AuditContext GetCurrent()
    {
        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return AuditContext.System;
        }

        ClaimsPrincipal? user = httpContext.User;
        bool isAuthenticated = user?.Identity?.IsAuthenticated == true;

        AuditActorType actorType = isAuthenticated
            ? user!.IsInRole(AdminRole) ? AuditActorType.AdminUser : AuditActorType.Customer
            : AuditActorType.Anonymous;

        return new AuditContext(
            ActorType: actorType,
            ActorId: isAuthenticated ? ResolveActorId(user!) : null,
            IpAddress: httpContext.Connection.RemoteIpAddress?.ToString(),
            // Set by CorrelationIdMiddleware, so an audit row can be joined
            // back to the request's structured logs.
            CorrelationId: httpContext.TraceIdentifier);
    }

    private static Guid? ResolveActorId(ClaimsPrincipal user)
    {
        string? subject = user.FindFirstValue(ClaimTypes.NameIdentifier);

        // A non-Guid subject means the token was issued by something we do not
        // model yet; record the action without a parseable actor id rather than
        // throwing inside an audit path.
        return Guid.TryParse(subject, out Guid actorId) ? actorId : null;
    }
}
