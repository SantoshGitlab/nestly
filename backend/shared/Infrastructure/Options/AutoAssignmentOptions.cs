using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "AutoAssignment" configuration section
/// (PROVIDER.md OPEN DECISIONS - AUTOMATIC ASSIGNMENT, tasks 247/248). No
/// admin UI to manage this yet, same not-yet-adminable-policy-knob reasoning
/// as <see cref="CommissionOptions"/>.
/// </summary>
public class AutoAssignmentOptions
{
    public const string SectionName = "AutoAssignment";

    /// <summary>
    /// Task 248's kill switch: when false, <c>ProviderAutoAssignmentHandler</c>
    /// takes no action at all on any booking - falls back to today's fully
    /// manual admin-assignment flow, with no other behaviour change. Default
    /// true (same as <see cref="Nestly.Infrastructure.Options.BackgroundJobOptions.ServerEnabled"/>'s
    /// convention: on by default, an explicit override to turn off) - lets
    /// ops disable a misbehaving first-release auto-dispatch engine in
    /// production without a deploy, purely via configuration.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Task 247: how many times <c>ProviderAutoAssignmentHandler</c> retries
    /// the next-best candidate after a rejection before leaving the booking
    /// for the manual admin queue. Decision 6: 3 - past a handful of
    /// declines the pattern is more likely a genuinely hard-to-place booking
    /// than one more retry fixing it, and the number itself has no
    /// production data behind it yet, hence configurable rather than a
    /// hardcoded constant.
    /// </summary>
    [Range(0, 20)]
    public int RetryAttempts { get; set; } = 3;
}
