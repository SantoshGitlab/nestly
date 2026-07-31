using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Identity;

public interface IAdminLoginService
{
    Task<Result<AdminLoginResponse>> LoginAsync(AdminLoginRequest request);

    /// <summary>Administrative override that clears an account lockout immediately (task 95d).</summary>
    Task<Result> UnlockAsync(Guid adminUserId);
}
