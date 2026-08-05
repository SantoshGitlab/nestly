using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "BusinessTime" configuration section: the
/// single timezone the platform's slot windows, slot dates and cutoff
/// policies are expressed in.
///
/// Deliberately configuration rather than the host's local timezone: the API
/// runs in containers whose OS timezone is UTC, so relying on
/// <c>DateTime.Now</c> would silently move every cutoff by the deployment
/// environment's timezone. Deliberately platform-wide rather than per-city:
/// no timezone column exists anywhere in the geography schema (see
/// <c>SlotAvailabilityService</c>'s doc comment) - add one, and resolve this
/// per city, if the platform ever spans multiple timezones.
/// </summary>
public class BusinessTimeOptions
{
    public const string SectionName = "BusinessTime";

    /// <summary>IANA timezone id the business operates in (e.g. "Asia/Kolkata"). Validated at startup - an unknown id fails fast rather than silently falling back to UTC.</summary>
    [Required]
    public string TimeZoneId { get; set; } = "Asia/Kolkata";
}
