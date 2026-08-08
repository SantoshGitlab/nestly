using System.Text.Json;
using System.Text.Json.Serialization;
using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Application.Notifications;

/// <summary>
/// Writes a domain event down and reads it back (task 294) - the payload a
/// <c>NotificationIntent</c> carries so the sweep can re-run the same handler
/// the in-process path ran, without re-deriving anything from the current
/// state of the database.
/// </summary>
/// <remarks>
/// <para>
/// <b>The round trip has to be faithful, not merely lossless-looking.</b> The
/// deduplication key is rebuilt from the deserialized event's
/// <see cref="IDomainEvent.EventId"/>; if that came back different, the sweep
/// would compute a key the in-process path never wrote and re-send an already
/// delivered message. <c>DomainEvent</c> declares <c>EventId</c>/
/// <c>OccurredOnUtc</c> with <c>init</c> accessors precisely so
/// <see cref="JsonSerializer"/> can restore them after invoking the record's
/// positional constructor.
/// </para>
/// <para>
/// <b>Enums are written as ordinals</b>, matching how this codebase already
/// puts enums on the wire (see <c>NotificationEventType</c>'s
/// append-never-insert note). Payloads are short-lived - a sweep picks an
/// intent up within minutes - but the same rule applies to them: appending an
/// enum member is safe, reordering one silently rewrites the meaning of every
/// unswept payload.
/// </para>
/// <para>
/// <b>Deserialization targets are allow-listed</b> by
/// <see cref="NotificationIntentPlanner.ResolveEventType"/>; nothing here
/// resolves a type from the stored string directly.
/// </para>
/// </remarks>
public static class DomainEventPayloadSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public static string Serialize(IDomainEvent domainEvent) =>
        JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), Options);

    /// <summary>Rehydrates a payload into <paramref name="eventType"/>, which must have come from <see cref="NotificationIntentPlanner.ResolveEventType"/>.</summary>
    public static IDomainEvent? Deserialize(string payloadJson, Type eventType) =>
        JsonSerializer.Deserialize(payloadJson, eventType, Options) as IDomainEvent;
}
