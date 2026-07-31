using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "ReviewPolicy" configuration section (SRS
/// 11.16.3 "review submission may be time-limited", task 85c). Same
/// not-yet-adminable policy-knob approach as <see cref="CancellationPolicyOptions"/>.
/// </summary>
public class ReviewPolicyOptions
{
    public const string SectionName = "ReviewPolicy";

    /// <summary>Whether a submission window is enforced at all - false means reviews can be submitted at any time after completion.</summary>
    public bool EnforceSubmissionWindow { get; set; } = true;

    /// <summary>Days after a booking completes during which it may still be reviewed, when <see cref="EnforceSubmissionWindow"/> is true.</summary>
    [Range(1, 3650)]
    public int SubmissionWindowDays { get; set; } = 30;
}
