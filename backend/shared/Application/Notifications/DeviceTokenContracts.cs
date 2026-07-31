using Nestly.Domain;

namespace Nestly.Application.Notifications;

public record RegisterDeviceTokenRequest(DevicePlatform Platform, string Token);

public record DeviceTokenResponse(Guid Id, DevicePlatform Platform, string Token, bool IsActive, DateTime RegisteredAtUtc);
