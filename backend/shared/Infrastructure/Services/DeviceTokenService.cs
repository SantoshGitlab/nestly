using Nestly.Application.Notifications;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Device token registration for push delivery (SRS 19.1, task 156).</summary>
public class DeviceTokenService : IDeviceTokenService
{
    private readonly IDeviceTokenRepository _repository;

    public DeviceTokenService(IDeviceTokenRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<DeviceTokenResponse>> RegisterAsync(Guid customerId, RegisterDeviceTokenRequest request)
    {
        var existing = await _repository.GetByTokenAsync(request.Token);
        if (existing is null)
        {
            var created = new DeviceToken(Guid.NewGuid(), customerId, request.Platform, request.Token);
            await _repository.AddAsync(created);
            return Result.Success(ToResponse(created));
        }

        // Re-registration (same customer, e.g. app reopened) and reassignment
        // (a different customer signed in on this device) both just
        // reactivate the existing row against the caller - see DeviceToken's
        // doc comment.
        existing.ReRegister(customerId, request.Platform);
        await _repository.UpdateAsync(existing);
        return Result.Success(ToResponse(existing));
    }

    public async Task<Result> RevokeAsync(Guid customerId, Guid deviceTokenId)
    {
        var token = await _repository.GetByIdAsync(deviceTokenId);
        if (token is null || token.CustomerId != customerId)
        {
            return Result.Failure(Error.NotFound("DeviceToken.NotFound", "The specified device token does not exist."));
        }

        token.Revoke();
        await _repository.UpdateAsync(token);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<DeviceTokenResponse>>> ListAsync(Guid customerId)
    {
        var tokens = await _repository.ListActiveByCustomerAsync(customerId);
        IReadOnlyList<DeviceTokenResponse> response = tokens.Select(ToResponse).ToList();
        return Result.Success(response);
    }

    private static DeviceTokenResponse ToResponse(DeviceToken token) => new(
        token.Id, token.Platform, token.Token, token.IsActive, token.RegisteredAtUtc);
}
