using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "Commission" configuration section (task
/// 157). There is no admin auth/UI to manage commission rates yet (that's
/// Phase 6) - the platform's commission percentage is configured here
/// instead via the Options pattern, same approach this codebase already
/// uses for other not-yet-adminable policy knobs (see <see cref="AccountOptions"/>).
/// </summary>
public class CommissionOptions
{
    public const string SectionName = "Commission";

    /// <summary>
    /// Percentage of a booking's payable amount retained as platform
    /// commission, applied whenever no per-category override in
    /// <see cref="CategoryRateOverrides"/> matches the booking's category (0-100).
    /// </summary>
    [Range(0, 100)]
    public decimal DefaultRatePercentage { get; set; } = 15m;

    /// <summary>
    /// Per-category rate overrides (0-100), keyed by CategoryId as a GUID
    /// string - e.g. <c>"3fa85f64-5717-4562-b3fc-2c963f66afa6": 20</c>. A
    /// category not listed here falls back to <see cref="DefaultRatePercentage"/>.
    /// </summary>
    public Dictionary<string, decimal> CategoryRateOverrides { get; set; } = new();
}
