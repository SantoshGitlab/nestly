using Microsoft.AspNetCore.Authorization;

namespace Nestly.Infrastructure.Authorization;

/// <summary>
/// An ASP.NET Core authorization requirement satisfied by one admin
/// permission code (SRS 12.2.3, task 96b) — e.g. <c>[Authorize(Policy =
/// "catalog.write")]</c> registers one of these per policy. See
/// <see cref="PermissionAuthorizationHandler"/> for how it's evaluated.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionCode)
    {
        PermissionCode = string.IsNullOrWhiteSpace(permissionCode)
            ? throw new ArgumentException("Permission code is required.", nameof(permissionCode))
            : permissionCode;
    }

    public string PermissionCode { get; }
}
