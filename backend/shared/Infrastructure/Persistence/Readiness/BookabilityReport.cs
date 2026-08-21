namespace Nestly.Infrastructure.Persistence.Readiness;

/// <summary>
/// One broken link in the chain a customer has to traverse to book anything.
/// <paramref name="Code"/> is stable and machine-readable (it is what the
/// health endpoint keys its <c>data</c> dictionary on and what an alert rule
/// would match); <paramref name="Remedy"/> is the sentence an operator needs.
/// </summary>
public sealed record BookabilityGap(string Code, string Remedy)
{
    public static BookabilityGap NoActiveCity { get; } = new(
        "bookability.no_active_city",
        "No active row in `city`. Geography has never been seeded, so no address resolves to anywhere the platform serves.");

    public static BookabilityGap NoActivePincode { get; } = new(
        "bookability.no_active_pincode",
        "No active `pincode` in any active city. Serviceability is mapped at pincode level, so nothing can be serviceable.");

    public static BookabilityGap NoLocality { get; } = new(
        "bookability.no_locality",
        "No active `locality` on any active pincode. A customer address is linked to geography by locality, and the slot API is entered by locality id, so slots can never be looked up.");

    public static BookabilityGap NoActiveService { get; } = new(
        "bookability.no_active_service",
        "No active `service`. The catalog is empty.");

    public static BookabilityGap NoServicePincodeMapping { get; } = new(
        "bookability.no_service_pincode_mapping",
        "No active `service_pincode_mapping` joining an active service to a pincode that has a locality. Every service reads as not serviceable everywhere - which is what the API correctly reports, with no error.");

    public static BookabilityGap NoSlotWindow { get; } = new(
        "bookability.no_slot_window",
        "No active `slot_window` in any active city. No city offers any time-of-day booking window.");

    public static BookabilityGap NoSlotWindowRule { get; } = new(
        "bookability.no_slot_window_rule",
        "Active slot windows exist but none has a `slot_window_rule` row. A window with no day-of-week rule is configured but never offered on any date, so the slot picker is always empty.");

    public static BookabilityGap ChainDisjoint { get; } = new(
        "bookability.chain_disjoint",
        "Every ingredient exists, but no single city holds all of them at once: the city with the serviceable pincode is not the city with the scheduled slot windows. Check that `slot_window.city_id` and `pincode.city_id` line up for at least one city.");

    public static BookabilityGap NoCategoryCityMapping { get; } = new(
        "bookability.no_category_city_mapping",
        "A service is bookable but its category is not listed in that city (`category_city_mapping`) or the category is inactive, so no customer can navigate to it from the app. The booking API would accept it; the browse UI never offers it.");
}

/// <summary>
/// The answer to "can anyone actually book anything against this database?",
/// produced by <see cref="BookabilityProbe"/>.
/// </summary>
/// <param name="IsBookable">
/// At least one active service is reachable from at least one addressable
/// pincode in a city that offers at least one slot window scheduled on at
/// least one day of the week - i.e. the chain
/// <see cref="Nestly.Infrastructure.Services.SlotAvailabilityService"/> walks
/// can return a non-empty answer for some (service, locality, date).
/// </param>
/// <param name="IsDiscoverable">
/// Additionally, a customer browsing the app can reach that service: its
/// category is active and mapped into that city, which is what
/// <c>CategoryRepository.ListServiceableInCityAsync</c> filters on.
/// </param>
/// <param name="Gaps">Empty when ready; otherwise every broken link found, outermost first.</param>
public sealed record BookabilityReport(
    bool IsBookable,
    bool IsDiscoverable,
    IReadOnlyList<BookabilityGap> Gaps)
{
    /// <summary>A database in which a customer can find and book a service.</summary>
    public static BookabilityReport Ready { get; } = new(true, true, []);

    public bool IsReady => IsBookable && IsDiscoverable;

    /// <summary>
    /// One line for a log or a health-check description. Deliberately blunt:
    /// the failure mode this exists for is silent, and a message that reads
    /// like routine startup chatter would reproduce it.
    /// </summary>
    public string Describe() => IsReady
        ? "Bookability check passed: at least one active service is bookable and discoverable."
        : "NOTHING CAN BE BOOKED against this database - every slot and serviceability API will keep returning correct, empty answers until it is bootstrapped. "
          + string.Join(" ", Gaps.Select(gap => $"[{gap.Code}] {gap.Remedy}"));
}
