using Nestly.Application.Identity;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Default MFA hook (task 95f): always succeeds. Registered so
/// <see cref="AdminLoginService"/> can call an <see cref="IAdminMfaChallengeProvider"/>
/// unconditionally without every environment needing a real MFA provider
/// configured — see the interface's doc comment for the full rationale.
/// </summary>
public class NoOpAdminMfaChallengeProvider : IAdminMfaChallengeProvider
{
    public Task<Result> VerifyAsync(AdminUser adminUser, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());
}
