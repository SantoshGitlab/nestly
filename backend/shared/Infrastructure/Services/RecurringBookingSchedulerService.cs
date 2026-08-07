using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.Notifications;
using Nestly.Application.Pricing;
using Nestly.Application.RecurringBookings;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// The Hangfire recurring job for task 185. Runs on a daily cron
/// (registered in <c>BackgroundJobRegistration</c>) and, for every active
/// plan due within <see cref="RecurringBookingOptions.LeadTimeDays"/>, calls
/// <see cref="IBookingService.CreateAsync"/> - the exact orchestration a
/// customer's own "Book now" tap uses (task 58) - never a second,
/// parallel booking-creation path. A failure (slot gone, address gone,
/// service deactivated, anything the orchestration itself rejects) is
/// treated as "skip and notify", never a silent drop and never an attempt to
/// pick a different slot on the customer's behalf (PRODUCT-ENHANCEMENTS.md
/// section 2).
/// </summary>
public class RecurringBookingSchedulerService : IRecurringBookingSchedulerService
{
    /// <summary>Codes <c>BookingSummaryService</c>/<c>BookingService</c> return for a slot/availability rejection specifically, as opposed to some other orchestration failure - classified separately in the occurrence log and used to decide notification wording, though today both outcomes dispatch the same <see cref="NotificationEventType.RecurringBookingSkipped"/> event.</summary>
    private static readonly HashSet<string> SlotUnavailableErrorCodes =
    [
        "Booking.NotServiceable",
        "Booking.SlotNotAvailable",
        "Booking.SlotCapacityReached"
    ];

    private readonly IRecurringBookingPlanRepository _planRepository;
    private readonly IRecurringBookingOccurrenceRepository _occurrenceRepository;
    private readonly IBookingService _bookingService;
    private readonly ICustomerRepository _customerRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly ISlotWindowRepository _slotWindowRepository;
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly INotificationDispatchService _notificationDispatchService;
    private readonly RecurringBookingOptions _options;
    private readonly ILogger<RecurringBookingSchedulerService> _logger;

    public RecurringBookingSchedulerService(
        IRecurringBookingPlanRepository planRepository,
        IRecurringBookingOccurrenceRepository occurrenceRepository,
        IBookingService bookingService,
        ICustomerRepository customerRepository,
        IServiceRepository serviceRepository,
        ISlotWindowRepository slotWindowRepository,
        IDeviceTokenRepository deviceTokenRepository,
        INotificationDispatchService notificationDispatchService,
        IOptions<RecurringBookingOptions> options,
        ILogger<RecurringBookingSchedulerService> logger)
    {
        _planRepository = planRepository;
        _occurrenceRepository = occurrenceRepository;
        _bookingService = bookingService;
        _customerRepository = customerRepository;
        _serviceRepository = serviceRepository;
        _slotWindowRepository = slotWindowRepository;
        _deviceTokenRepository = deviceTokenRepository;
        _notificationDispatchService = notificationDispatchService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProcessDueOccurrencesAsync(CancellationToken cancellationToken)
    {
        var horizon = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(_options.LeadTimeDays);
        var duePlans = await _planRepository.ListDueAsync(horizon);

        _logger.LogInformation("Recurring booking scheduler sweep: {Count} plan(s) due on or before {Horizon}.", duePlans.Count, horizon);

        foreach (var plan in duePlans)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await ProcessPlanAsync(plan, cancellationToken);
            }
            catch (Exception ex)
            {
                // One plan's unexpected failure must not abort the sweep for
                // every other plan in the batch - logged with full detail
                // (server-side only, never surfaced to a customer) and left
                // for the next run/retry to pick back up, since the plan's
                // NextOccurrenceDate was never advanced.
                _logger.LogError(ex, "Unexpected failure processing recurring plan {PlanId}.", plan.Id);
            }
        }
    }

    private async Task ProcessPlanAsync(RecurringBookingPlan plan, CancellationToken cancellationToken)
    {
        var occurrenceDate = plan.NextOccurrenceDate;

        // Idempotency guard (BackgroundJobRegistration: "jobs must be
        // idempotent - a retry re-runs the whole method"). A prior run that
        // got as far as recording the occurrence but crashed before saving
        // the plan's advanced pointer would otherwise double-book on retry.
        if (await _occurrenceRepository.ExistsForDateAsync(plan.Id, occurrenceDate))
        {
            _logger.LogWarning(
                "Recurring plan {PlanId} already has a recorded occurrence for {ScheduledDate}; skipping re-processing.",
                plan.Id, occurrenceDate);
            return;
        }

        var addOns = plan.AddOns.Select(a => new AddOnSelection(a.AddOnId, a.Quantity)).ToList();
        var request = new BookingSummaryRequest(
            plan.ServiceId, plan.CityId, plan.AddressId, plan.LocalityId, plan.SlotWindowId,
            occurrenceDate, plan.Quantity, addOns);

        Result<BookingDetailResponse> result = await _bookingService.CreateAsync(plan.CustomerId, request);

        if (result.IsSuccess)
        {
            await RecordOccurrenceAsync(plan, occurrenceDate, RecurringBookingOccurrenceOutcome.Booked, result.Value.Id, null, cancellationToken);
            plan.RecordOccurrenceBooked(occurrenceDate);
        }
        else
        {
            var outcome = SlotUnavailableErrorCodes.Contains(result.Error.Code)
                ? RecurringBookingOccurrenceOutcome.SkippedSlotUnavailable
                : RecurringBookingOccurrenceOutcome.SkippedOrchestrationRejected;

            _logger.LogWarning(
                "Recurring plan {PlanId} occurrence for {ScheduledDate} skipped ({ErrorCode}): {ErrorMessage}",
                plan.Id, occurrenceDate, result.Error.Code, result.Error.Message);

            await RecordOccurrenceAsync(plan, occurrenceDate, outcome, null, result.Error.Message, cancellationToken);
            plan.RecordOccurrenceSkipped(occurrenceDate);
        }

        await _planRepository.UpdateAsync(plan);
    }

    private async Task RecordOccurrenceAsync(
        RecurringBookingPlan plan,
        DateOnly occurrenceDate,
        RecurringBookingOccurrenceOutcome outcome,
        Guid? bookingId,
        string? skipReason,
        CancellationToken cancellationToken)
    {
        var occurrence = new RecurringBookingOccurrence(Guid.NewGuid(), plan.Id, occurrenceDate, outcome, bookingId, skipReason);
        await _occurrenceRepository.AddAsync(occurrence);

        await NotifyAsync(plan, occurrenceDate, outcome, bookingId, cancellationToken);
    }

    private async Task NotifyAsync(
        RecurringBookingPlan plan,
        DateOnly occurrenceDate,
        RecurringBookingOccurrenceOutcome outcome,
        Guid? bookingId,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(plan.CustomerId);
        if (customer is null)
        {
            _logger.LogWarning("Recurring plan {PlanId}'s customer {CustomerId} was not found; skipping notification.", plan.Id, plan.CustomerId);
            return;
        }

        var service = await _serviceRepository.GetByIdAsync(plan.ServiceId);
        var slotWindow = await _slotWindowRepository.GetByIdAsync(plan.SlotWindowId);
        var deviceTokens = await _deviceTokenRepository.ListActiveByOwnerAsync(DeviceTokenOwner.ForCustomer(plan.CustomerId));
        var recipient = new NotificationRecipient(customer.Mobile, customer.Email, deviceTokens.Select(t => t.Token).ToList());

        var variables = new Dictionary<string, string>
        {
            ["CustomerName"] = customer.Name,
            ["ServiceName"] = service?.Name ?? string.Empty,
            ["SlotDate"] = occurrenceDate.ToString("yyyy-MM-dd"),
            ["SlotWindow"] = slotWindow?.Name ?? string.Empty
        };

        var eventType = outcome == RecurringBookingOccurrenceOutcome.Booked
            ? NotificationEventType.RecurringBookingUpcoming
            : NotificationEventType.RecurringBookingSkipped;

        await _notificationDispatchService.DispatchAsync(
            plan.CustomerId, eventType, recipient, variables, bookingId: bookingId, cancellationToken: cancellationToken);
    }
}
