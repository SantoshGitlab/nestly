using Microsoft.Extensions.Options;
using Nestly.Application.Abstractions.Time;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// <see cref="IBusinessClock"/> over the injected <see cref="TimeProvider"/>
/// and the configured <see cref="BusinessTimeOptions.TimeZoneId"/>, so tests
/// keep full control of "now" while production reads a real clock.
///
/// The timezone is resolved once in the constructor: an unknown id is a
/// configuration error that should surface at startup, not on the first
/// customer's slot lookup.
/// </summary>
public class BusinessClock : IBusinessClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public BusinessClock(TimeProvider timeProvider, IOptions<BusinessTimeOptions> options)
    {
        _timeProvider = timeProvider;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
    }

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, _timeZone);

    public DateOnly Today => DateOnly.FromDateTime(Now);

    public DateTime ToUtc(DateOnly date, TimeSpan timeOfDay)
    {
        // Unspecified rather than Local: the value is business-local, which
        // is not the host's local timezone, and ConvertTimeToUtc rejects a
        // DateTime whose Kind contradicts the zone it is given.
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).Add(timeOfDay), DateTimeKind.Unspecified);

        // A wall-clock time inside a DST spring-forward gap never happens, and
        // ConvertTimeToUtc throws on it. Asia/Kolkata has no DST so this is
        // unreachable today, but a slot window configured across such a gap
        // must not take down slot availability for the whole city - treat it
        // as the first instant that does exist after the gap.
        if (_timeZone.IsInvalidTime(local))
        {
            var adjustment = _timeZone.GetAdjustmentRules()
                .FirstOrDefault(rule => local >= rule.DateStart && local <= rule.DateEnd);
            local = local.Add(adjustment?.DaylightDelta ?? TimeSpan.FromHours(1));
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, _timeZone);
    }
}
