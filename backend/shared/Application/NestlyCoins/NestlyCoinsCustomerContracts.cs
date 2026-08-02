namespace Nestly.Application.NestlyCoins;

/// <summary>
/// Public earn-rate info for in-app messaging (docs/NESTLY-COINS.md API
/// SURFACE, task 203) - deliberately narrower than the admin-facing
/// <c>NestlyCoinsProgramConfigResponse</c>: no <c>MaxCoinsPerMonth</c> or
/// <c>ClawbackWindowDays</c> (internal fraud-prevention policy, not customer
/// messaging), no <c>Id</c>/<c>UpdatedAtUtc</c>/<c>UpdatedByAdminUserId</c>
/// (admin bookkeeping, not something a customer needs).
/// </summary>
public sealed record NestlyCoinsProgramPublicResponse(
    decimal EarnRatePer100,
    decimal MinimumOrderAmount,
    bool RequireReorder,
    int ExpiryDays);
