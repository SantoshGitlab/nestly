using Microsoft.EntityFrameworkCore;
using Nestly.Application.Bookings;
using Nestly.Application.ProviderManagement;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IProviderMatchingService"/>.</summary>
public class ProviderMatchingService : IProviderMatchingService
{
    private const double EarthRadiusKm = 6371.0;

    private readonly IBookingRepository _bookingRepository;
    private readonly NestlyDbContext _context;

    public ProviderMatchingService(IBookingRepository bookingRepository, NestlyDbContext context)
    {
        _bookingRepository = bookingRepository;
        _context = context;
    }

    public async Task<IReadOnlyList<ProviderMatchCandidate>> FindCandidatesAsync(Guid bookingId, IReadOnlyCollection<Guid>? excludeProviderIds = null)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null || booking.Items.Count == 0)
        {
            return [];
        }

        var serviceId = booking.Items[0].ServiceId;
        var service = await _context.Set<Service>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceId);
        var slotWindow = await _context.Set<SlotWindow>().AsNoTracking().FirstOrDefaultAsync(w => w.Id == booking.SlotWindowId);
        if (service is null || slotWindow is null)
        {
            return [];
        }

        // Pincode.Code is globally unique (IPincodeRepository's own doc
        // comment), so this resolves unambiguously without needing the
        // city context. Null if the snapshot's pincode text no longer
        // matches any master row (stale/free-text drift) - candidates
        // whose area is pincode-scoped simply can't be verified and are
        // excluded below, same as a real mismatch.
        var pincode = await _context.Set<Pincode>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == booking.AddressPincodeSnapshot);
        Guid? pincodeId = pincode?.Id;

        var excluded = excludeProviderIds ?? [];

        // ponytail: zone-level ProviderServiceArea scoping is not checked -
        // Booking has no LocalityId (only free-text city/pincode snapshots),
        // and Zone is only reachable through Locality, so there is no
        // reliable way to resolve which zone a booking's address falls in
        // from what Booking actually stores today. City + pincode (both
        // resolvable) are enforced; upgrade path is snapshotting
        // Booking.LocalityId at creation, the same way SourceAddressId
        // already is, if zone-scoped coverage turns out to matter in
        // practice.
        var providers = await _context.Set<Provider>()
            .AsNoTracking()
            .Where(p => p.Status == ProviderStatus.Active && !excluded.Contains(p.Id))
            .Where(p => _context.Set<ProviderSkillMapping>().Any(s =>
                s.ProviderId == p.Id && s.IsActive && s.CategoryId == service.CategoryId &&
                (s.ServiceId == null || s.ServiceId == serviceId)))
            .Where(p => _context.Set<ProviderServiceArea>().Any(a =>
                a.ProviderId == p.Id && a.IsActive && a.CityId == slotWindow.CityId &&
                (a.PincodeId == null || a.PincodeId == pincodeId)))
            .ToListAsync();

        var candidates = providers.Select(p => new ProviderMatchCandidate(
            p.Id,
            DistanceKm(booking.AddressLatitudeSnapshot, booking.AddressLongitudeSnapshot, p.Latitude, p.Longitude)));

        // Candidates with a known distance first (nearest first); a
        // candidate either side couldn't be measured for (no coordinates on
        // one or both ends) sorts after all of those, ordered only by Id for
        // a deterministic result - not a ranking signal (PROVIDER.md OPEN
        // DECISIONS - AUTOMATIC ASSIGNMENT #3: no rating input exists to
        // break ties with instead).
        return candidates
            .OrderBy(c => c.DistanceKm.HasValue ? 0 : 1)
            .ThenBy(c => c.DistanceKm)
            .ThenBy(c => c.ProviderId)
            .ToList();
    }

    /// <summary>Great-circle (Haversine) distance in kilometres - PROVIDER.md OPEN DECISIONS - AUTOMATIC ASSIGNMENT #1: no PostGIS in this solution, so this is plain <see cref="Math"/> over the two decimal(9,6) coordinate pairs.</summary>
    private static decimal? DistanceKm(decimal? lat1, decimal? lon1, decimal? lat2, decimal? lon2)
    {
        if (lat1 is null || lon1 is null || lat2 is null || lon2 is null)
        {
            return null;
        }

        double dLat = ToRadians((double)(lat2.Value - lat1.Value));
        double dLon = ToRadians((double)(lon2.Value - lon1.Value));
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians((double)lat1.Value)) * Math.Cos(ToRadians((double)lat2.Value)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return (decimal)(EarthRadiusKm * c);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
