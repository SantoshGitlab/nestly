using Microsoft.EntityFrameworkCore;
using Nestly.Application.ProviderManagement;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IProviderActiveJobLimitService"/>.</summary>
public class ProviderActiveJobLimitService : IProviderActiveJobLimitService
{
    // Same cross-aggregate join as ProviderScheduleConflictService/
    // ProviderTravelFeasibilityService: who is committed lives on
    // BookingProviderAssignment, the fulfilment state lives on Booking.
    private readonly NestlyDbContext _context;

    private static readonly BookingStatus[] ActiveJobStatuses =
    [
        BookingStatus.ProviderEnRoute,
        BookingStatus.ProviderArrived,
        BookingStatus.InProgress
    ];

    public ProviderActiveJobLimitService(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<bool> HasAnotherActiveJobAsync(Guid providerId, Guid excludingBookingId, CancellationToken cancellationToken = default) =>
        _context.Set<BookingProviderAssignment>()
            .Join(_context.Set<Booking>(), a => a.BookingId, b => b.Id, (a, b) => new { Assignment = a, Booking = b })
            .AnyAsync(x =>
                x.Assignment.ProviderId == providerId &&
                x.Assignment.Status == BookingProviderAssignmentStatus.Accepted &&
                x.Booking.Id != excludingBookingId &&
                ActiveJobStatuses.Contains(x.Booking.Status),
                cancellationToken);
}
