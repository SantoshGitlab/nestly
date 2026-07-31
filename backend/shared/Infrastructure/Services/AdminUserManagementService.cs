using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.AdminUserManagement;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Admin user management (SRS 12.2.1, tasks 97a-97d): CRUD, role assignment,
/// activate/deactivate, and admin-initiated password reset over
/// <see cref="AdminUser"/>. Reuses the same <see cref="PasswordHasher{TUser}"/>
/// the self-service admin login path uses (<see cref="AdminLoginService"/>)
/// rather than a second hashing scheme, and writes an audit entry for every
/// mutation - managing another admin's account is a security-sensitive
/// action (SRS 21), the same reasoning <see cref="AdminLoginService"/> and
/// <c>ReviewModerationService</c> already apply to their own writes.
/// </summary>
public class AdminUserManagementService : IAdminUserManagementService
{
    /// <summary>
    /// Characters a generated temporary password is drawn from (task 97d) -
    /// excludes visually ambiguous characters (0/O, 1/l/I) since the result
    /// is meant to be read aloud or retyped by whoever receives it out of
    /// band, and always includes at least one of each class the shared
    /// <see cref="Identity.AdminPasswordPolicy"/> requires so the generated
    /// value never fails its own validation.
    /// </summary>
    private const string UpperChars = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string LowerChars = "abcdefghijkmnpqrstuvwxyz";
    private const string DigitChars = "23456789";
    private const string SpecialChars = "!@#$%^&*-_+=";
    private const int TemporaryPasswordLength = 16;

    private readonly IAdminUserRepository _adminUserRepository;
    private readonly IAdminRoleRepository _adminRoleRepository;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly NestlyDbContext _dbContext;
    private readonly PasswordHasher<AdminUser> _passwordHasher = new();

    public AdminUserManagementService(
        IAdminUserRepository adminUserRepository,
        IAdminRoleRepository adminRoleRepository,
        IAuditLogWriter auditLogWriter,
        NestlyDbContext dbContext)
    {
        _adminUserRepository = adminUserRepository;
        _adminRoleRepository = adminRoleRepository;
        _auditLogWriter = auditLogWriter;
        _dbContext = dbContext;
    }

    public async Task<Result<AdminUserSearchResponse>> SearchAsync(AdminUserSearchRequest request)
    {
        var filter = new AdminUserSearchFilter(
            request.Email, request.Name, request.Status, request.RoleId, request.Page, request.PageSize);

        var result = await _adminUserRepository.SearchAsync(filter);
        DateTime now = DateTime.UtcNow;

        var items = result.Rows
            .Select(row => new AdminUserSummaryResponse(
                row.AdminUser.Id,
                row.AdminUser.Email,
                row.AdminUser.FullName,
                row.AdminUser.Status,
                row.AdminUser.RoleId,
                row.RoleName,
                row.AdminUser.IsLockedOut(now),
                row.AdminUser.CreatedAt))
            .ToList();

        return new AdminUserSearchResponse(items, result.TotalCount, request.Page, request.PageSize);
    }

    public async Task<Result<AdminUserDetailResponse>> GetByIdAsync(Guid adminUserId)
    {
        var adminUser = await _adminUserRepository.GetByIdAsync(adminUserId);
        if (adminUser is null)
        {
            return NotFound();
        }

        return await BuildDetailAsync(adminUser);
    }

    public async Task<Result<AdminUserDetailResponse>> CreateAsync(CreateAdminUserRequest request, Guid actingAdminUserId)
    {
        var existing = await _adminUserRepository.GetByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Error.Conflict("AdminUser.EmailInUse", "An admin account with that email already exists.");
        }

        if (request.RoleId.HasValue && !await _adminRoleRepository.ExistsAsync(request.RoleId.Value))
        {
            return Error.NotFound("AdminRole.NotFound", "The specified role does not exist.");
        }

        var adminUser = new AdminUser(Guid.NewGuid(), request.Email, "placeholder", request.FullName);
        string passwordHash = _passwordHasher.HashPassword(adminUser, request.Password);
        adminUser.SetPasswordHash(passwordHash);

        if (request.RoleId.HasValue)
        {
            adminUser.AssignRole(request.RoleId.Value);
        }

        await _adminUserRepository.AddAsync(adminUser);
        await WriteAuditAsync(adminUser.Id, "AdminUserCreated", actingAdminUserId);

        return await BuildDetailAsync(adminUser);
    }

    public async Task<Result<AdminUserDetailResponse>> UpdateAsync(Guid adminUserId, UpdateAdminUserRequest request, Guid actingAdminUserId)
    {
        var adminUser = await _adminUserRepository.GetByIdAsync(adminUserId);
        if (adminUser is null)
        {
            return NotFound();
        }

        if (!string.Equals(adminUser.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _adminUserRepository.GetByEmailAsync(request.Email);
            if (existing is not null && existing.Id != adminUserId)
            {
                return Error.Conflict("AdminUser.EmailInUse", "An admin account with that email already exists.");
            }

            adminUser.ChangeEmail(request.Email);
        }

        adminUser.UpdateProfile(request.FullName);
        await _adminUserRepository.UpdateAsync(adminUser);
        await WriteAuditAsync(adminUser.Id, "AdminUserProfileUpdated", actingAdminUserId);

        return await BuildDetailAsync(adminUser);
    }

    public async Task<Result<AdminUserDetailResponse>> AssignRoleAsync(Guid adminUserId, AssignAdminRoleRequest request, Guid actingAdminUserId)
    {
        var adminUser = await _adminUserRepository.GetByIdAsync(adminUserId);
        if (adminUser is null)
        {
            return NotFound();
        }

        if (request.RoleId.HasValue && !await _adminRoleRepository.ExistsAsync(request.RoleId.Value))
        {
            return Error.NotFound("AdminRole.NotFound", "The specified role does not exist.");
        }

        adminUser.AssignRole(request.RoleId);
        await _adminUserRepository.UpdateAsync(adminUser);
        await WriteAuditAsync(adminUser.Id, "AdminUserRoleAssigned", actingAdminUserId);

        return await BuildDetailAsync(adminUser);
    }

    public async Task<Result<AdminUserDetailResponse>> ActivateAsync(Guid adminUserId, Guid actingAdminUserId)
    {
        var adminUser = await _adminUserRepository.GetByIdAsync(adminUserId);
        if (adminUser is null)
        {
            return NotFound();
        }

        adminUser.Activate();
        await _adminUserRepository.UpdateAsync(adminUser);
        await WriteAuditAsync(adminUser.Id, "AdminUserActivated", actingAdminUserId);

        return await BuildDetailAsync(adminUser);
    }

    public async Task<Result<AdminUserDetailResponse>> DeactivateAsync(Guid adminUserId, Guid actingAdminUserId)
    {
        var adminUser = await _adminUserRepository.GetByIdAsync(adminUserId);
        if (adminUser is null)
        {
            return NotFound();
        }

        if (adminUser.Id == actingAdminUserId)
        {
            return Error.Business("AdminUser.CannotDeactivateSelf", "You cannot deactivate your own account.");
        }

        adminUser.Deactivate();
        await _adminUserRepository.UpdateAsync(adminUser);
        await WriteAuditAsync(adminUser.Id, "AdminUserDeactivated", actingAdminUserId);

        return await BuildDetailAsync(adminUser);
    }

    public async Task<Result<ResetAdminPasswordResponse>> ResetPasswordAsync(Guid adminUserId, Guid actingAdminUserId)
    {
        var adminUser = await _adminUserRepository.GetByIdAsync(adminUserId);
        if (adminUser is null)
        {
            return NotFound();
        }

        string temporaryPassword = GenerateTemporaryPassword();
        string passwordHash = _passwordHasher.HashPassword(adminUser, temporaryPassword);
        adminUser.SetPasswordHash(passwordHash);
        await _adminUserRepository.UpdateAsync(adminUser);

        // Never write the temporary password itself into the audit trail
        // (SRS 21/28: audit rows must never carry credentials) - only that a
        // reset occurred.
        await WriteAuditAsync(adminUser.Id, "AdminUserPasswordReset", actingAdminUserId);

        return new ResetAdminPasswordResponse(temporaryPassword);
    }

    public async Task<Result<IReadOnlyList<AdminRoleSummaryResponse>>> ListRolesAsync()
    {
        var roles = await _adminRoleRepository.ListAllAsync();
        IReadOnlyList<AdminRoleSummaryResponse> response = roles
            .Select(r => new AdminRoleSummaryResponse(r.Id, r.Name, r.Description))
            .ToList();

        return Result.Success(response);
    }

    private async Task<AdminUserDetailResponse> BuildDetailAsync(AdminUser adminUser)
    {
        string? roleName = null;
        if (adminUser.RoleId.HasValue)
        {
            var role = await _adminRoleRepository.GetByIdAsync(adminUser.RoleId.Value);
            roleName = role?.Name;
        }

        DateTime now = DateTime.UtcNow;
        return new AdminUserDetailResponse(
            adminUser.Id,
            adminUser.Email,
            adminUser.FullName,
            adminUser.Status,
            adminUser.RoleId,
            roleName,
            adminUser.IsLockedOut(now),
            adminUser.LockedUntilUtc,
            adminUser.FailedLoginAttempts,
            adminUser.CreatedAt,
            adminUser.UpdatedAt);
    }

    private async Task WriteAuditAsync(Guid adminUserId, string action, Guid actingAdminUserId)
    {
        await _auditLogWriter.WriteAsync(new AuditEntry("AdminUser", adminUserId.ToString(), action));
        await _dbContext.SaveChangesAsync();
    }

    private static string GenerateTemporaryPassword()
    {
        // One guaranteed character from each class the shared password policy
        // requires, then the remainder from the full pool, shuffled - keeps
        // the result within AdminPasswordPolicy without a generate-and-retry
        // loop against it.
        string pool = UpperChars + LowerChars + DigitChars + SpecialChars;
        var chars = new List<char>
        {
            UpperChars[RandomNumberGenerator.GetInt32(UpperChars.Length)],
            LowerChars[RandomNumberGenerator.GetInt32(LowerChars.Length)],
            DigitChars[RandomNumberGenerator.GetInt32(DigitChars.Length)],
            SpecialChars[RandomNumberGenerator.GetInt32(SpecialChars.Length)],
        };

        for (int i = chars.Count; i < TemporaryPasswordLength; i++)
        {
            chars.Add(pool[RandomNumberGenerator.GetInt32(pool.Length)]);
        }

        // Fisher-Yates shuffle using a CSPRNG so the guaranteed characters
        // above are not always in the same leading positions.
        for (int i = chars.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars.ToArray());
    }

    private static Error NotFound() =>
        Error.NotFound("AdminUser.NotFound", "No admin account was found with that id.");
}
