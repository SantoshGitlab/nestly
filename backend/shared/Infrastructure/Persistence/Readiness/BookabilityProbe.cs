using Microsoft.EntityFrameworkCore;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Readiness;

/// <summary>
/// Task 389 (PRODUCTION-READINESS.md 5.1, QA-REPORT-2026-08-18 Phase 1):
/// answers "can anyone book anything against this database?" and, when the
/// answer is no, says which link of the chain is missing.
///
/// <para>
/// <b>Why this exists.</b> Phase 1 of the 2026-08-18 sweep found zero rows in
/// <c>service_pincode_mapping</c> and <c>slot_window_rule</c> for any seeded
/// city: no customer could book any service, in any environment built from
/// migrations plus the seed scripts. Nothing was broken.
/// <c>SlotAvailabilityService</c> and <c>SlotWindowRepository</c> fail closed
/// <em>by design</em> - an unmapped service/pincode, or a window with no rule
/// row, is correctly treated as unbookable - so the code was right, the
/// database was empty, and every API returned a correct, empty answer. That
/// combination is silent: no exception, no 5xx, no failing health check, no
/// error log. It was found by a human trying to book, which in production
/// means it is found by a customer.
/// </para>
///
/// <para>
/// <b>Why a probe rather than a seed migration.</b> Same reasoning as
/// <see cref="Seed.AdminPermissionReconciler"/> (task 332): reference data a
/// migration freezes at authoring time goes stale. Unlike admin permissions
/// there is not even a catalog in code to reconcile <em>against</em> - which
/// cities a deployment serves is a business decision, not a constant - so
/// this deliberately writes nothing. It only removes the silence, and points
/// at <c>database/seed/bootstrap-launch-city.sql</c>, which is the thing an
/// operator runs.
/// </para>
///
/// <para>
/// <b>The chain being checked</b> is the one
/// <see cref="Nestly.Infrastructure.Services.SlotAvailabilityService.GetAvailableSlotsAsync"/>
/// walks, not an invented approximation of it: locality to pincode to city
/// (<c>ServiceabilityRepository.GetCityAndPincodeForLocalityAsync</c>), an
/// active <c>service_pincode_mapping</c> for that (service, pincode)
/// (<c>IsServiceActiveInPincodeAsync</c>), and an active slot window in that
/// city carrying at least one day-of-week rule
/// (<c>SlotWindowRepository.ListActiveForCityAndDayAsync</c>). Everything past
/// that point is per-request state no bootstrap can pre-satisfy - the date
/// asked for, blackouts, cutoffs, remaining capacity - so this stops at "some
/// (service, locality, date) can return a slot" rather than pretending to
/// evaluate a particular booking.
/// </para>
///
/// <para>
/// Written entirely in LINQ, and deliberately so: the suites that cover it are
/// SQLite-backed, and provider-specific raw SQL would silently stop being
/// exercised (ORIENTATION.md 7, the notification-template filter). Read-only
/// and side-effect free, so it is safe to run on every health check.
/// </para>
/// </summary>
public sealed class BookabilityProbe
{
    private readonly NestlyDbContext _dbContext;

    public BookabilityProbe(NestlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Runs the check. The ready path costs two <c>EXISTS</c> queries; the
    /// per-link diagnosis only runs once something is already wrong, on a
    /// database that is by definition nearly empty.
    /// </summary>
    public async Task<BookabilityReport> InspectAsync(CancellationToken cancellationToken = default)
    {
        var chain = BuildChain();

        bool isBookable = await chain.BookableOfferingIds.AnyAsync(cancellationToken);
        bool isDiscoverable = isBookable && await chain.DiscoverableOfferingIds.AnyAsync(cancellationToken);

        if (isBookable && isDiscoverable)
        {
            return BookabilityReport.Ready;
        }

        IReadOnlyList<BookabilityGap> gaps = isBookable
            ? [BookabilityGap.NoCategoryCityMapping]
            : await DiagnoseAsync(chain, cancellationToken);

        return new BookabilityReport(isBookable, isDiscoverable, gaps);
    }

    /// <summary>
    /// Composes the chain as nested <see cref="IQueryable{T}"/>s held in
    /// locals, which is how the rest of this layer writes a correlated
    /// subquery (see <c>SlotWindowRepository.ListActiveForCityAndDayAsync</c>
    /// and <c>CategoryRepository.ListServiceableInCityAsync</c>): nothing is
    /// enumerated until a caller awaits one of them, so each await collapses
    /// into a single statement on both providers.
    /// </summary>
    private BookabilityChain BuildChain()
    {
        var activeCityIds = _dbContext.Set<City>()
            .Where(city => city.IsActive)
            .Select(city => city.Id);

        var activeServiceIds = _dbContext.Set<Service>()
            .Where(service => service.IsActive)
            .Select(service => service.Id);

        var activeCategoryIds = _dbContext.Set<Category>()
            .Where(category => category.IsActive)
            .Select(category => category.Id);

        var activePincodeIds = _dbContext.Set<Pincode>()
            .Where(pincode => pincode.IsActive && activeCityIds.Contains(pincode.CityId))
            .Select(pincode => pincode.Id);

        // A pincode no address can name is not reachable: a customer address
        // carries a locality id (CustomerAddress.LinkToGeography), and that
        // locality id is the only way into the slot API.
        var addressablePincodeIds = _dbContext.Set<Locality>()
            .Where(locality => locality.IsActive && activePincodeIds.Contains(locality.PincodeId))
            .Select(locality => locality.PincodeId);

        // "Scheduled", not merely "configured": SlotWindow's own doc comment
        // spells out that a window with no rules is not offered on any day,
        // and ListActiveForCityAndDayAsync filters on exactly this.
        var scheduledWindowIds = _dbContext.Set<SlotWindowRule>()
            .Select(rule => rule.SlotWindowId);

        var citiesWithScheduledWindows = _dbContext.Set<SlotWindow>()
            .Where(window => window.IsActive
                && activeCityIds.Contains(window.CityId)
                && scheduledWindowIds.Contains(window.Id))
            .Select(window => window.CityId);

        var bookableOfferingIds =
            from mapping in _dbContext.Set<ServicePincodeMapping>()
            join service in _dbContext.Set<Service>() on mapping.ServiceId equals service.Id
            join pincode in _dbContext.Set<Pincode>() on mapping.PincodeId equals pincode.Id
            where mapping.IsActive
                && service.IsActive
                && addressablePincodeIds.Contains(pincode.Id)
                && citiesWithScheduledWindows.Contains(pincode.CityId)
            select mapping.Id;

        // Bookable is not the same as findable, so this is a second query
        // rather than a filter over the first: the booking API would accept an
        // offering whose category is not mapped into that city, but
        // CategoryRepository.ListServiceableInCityAsync never lists it, so no
        // customer can navigate to the service to book it.
        var discoverableOfferingIds =
            from mapping in _dbContext.Set<ServicePincodeMapping>()
            join service in _dbContext.Set<Service>() on mapping.ServiceId equals service.Id
            join pincode in _dbContext.Set<Pincode>() on mapping.PincodeId equals pincode.Id
            join categoryCity in _dbContext.Set<CategoryCityMapping>()
                on new { service.CategoryId, pincode.CityId }
                equals new { categoryCity.CategoryId, categoryCity.CityId }
            where mapping.IsActive
                && service.IsActive
                && categoryCity.IsActive
                && activeCategoryIds.Contains(service.CategoryId)
                && addressablePincodeIds.Contains(pincode.Id)
                && citiesWithScheduledWindows.Contains(pincode.CityId)
            select mapping.Id;

        return new BookabilityChain(
            activeCityIds,
            activeServiceIds,
            activePincodeIds,
            addressablePincodeIds,
            scheduledWindowIds,
            bookableOfferingIds,
            discoverableOfferingIds);
    }

    /// <summary>
    /// Names every prerequisite that is outright absent. Reported together
    /// rather than one per restart: an operator bootstrapping a fresh
    /// deployment needs the whole list. When every prerequisite is present and
    /// the chain still yields nothing, the pieces exist but do not line up in
    /// any one city - a different problem with a different fix, so it gets its
    /// own gap rather than an empty list that would read as "ready".
    /// </summary>
    private async Task<IReadOnlyList<BookabilityGap>> DiagnoseAsync(
        BookabilityChain chain,
        CancellationToken cancellationToken)
    {
        var gaps = new List<BookabilityGap>();

        if (!await _dbContext.Set<City>().AnyAsync(city => city.IsActive, cancellationToken))
        {
            gaps.Add(BookabilityGap.NoActiveCity);
        }
        else if (!await chain.ActivePincodeIds.AnyAsync(cancellationToken))
        {
            gaps.Add(BookabilityGap.NoActivePincode);
        }
        else if (!await chain.AddressablePincodeIds.AnyAsync(cancellationToken))
        {
            gaps.Add(BookabilityGap.NoLocality);
        }

        if (!await chain.ActiveServiceIds.AnyAsync(cancellationToken))
        {
            gaps.Add(BookabilityGap.NoActiveService);
        }
        else if (!await _dbContext.Set<ServicePincodeMapping>()
                     .AnyAsync(
                         mapping => mapping.IsActive
                             && chain.ActiveServiceIds.Contains(mapping.ServiceId)
                             && chain.AddressablePincodeIds.Contains(mapping.PincodeId),
                         cancellationToken))
        {
            gaps.Add(BookabilityGap.NoServicePincodeMapping);
        }

        var activeWindows = _dbContext.Set<SlotWindow>()
            .Where(window => window.IsActive && chain.ActiveCityIds.Contains(window.CityId));

        if (!await activeWindows.AnyAsync(cancellationToken))
        {
            gaps.Add(BookabilityGap.NoSlotWindow);
        }
        else if (!await activeWindows.AnyAsync(
                     window => chain.ScheduledWindowIds.Contains(window.Id), cancellationToken))
        {
            // The exact shape QA found: slot windows configured, zero rules.
            gaps.Add(BookabilityGap.NoSlotWindowRule);
        }

        if (gaps.Count == 0)
        {
            gaps.Add(BookabilityGap.ChainDisjoint);
        }

        return gaps;
    }

    private sealed record BookabilityChain(
        IQueryable<Guid> ActiveCityIds,
        IQueryable<Guid> ActiveServiceIds,
        IQueryable<Guid> ActivePincodeIds,
        IQueryable<Guid> AddressablePincodeIds,
        IQueryable<Guid> ScheduledWindowIds,
        IQueryable<Guid> BookableOfferingIds,
        IQueryable<Guid> DiscoverableOfferingIds);
}
