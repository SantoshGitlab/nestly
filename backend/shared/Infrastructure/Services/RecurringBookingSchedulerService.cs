using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.Notifications;
using Nestly.Application.Pricing;
using Nestly.Application.ProviderManagement;
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
///
/// <b>Task 297 - the provider dimension.</b> Every generated booking now
/// carries <see cref="Booking.RecurringBookingPlanId"/> (task 296's FK),
/// passed through the same <see cref="IBookingService.CreateAsync"/> call
/// rather than stamped on afterwards. Once the booking exists, the generator
/// asks the question the row is really about: can the professional this
/// customer already knows serve this date? Provider assignment itself is not
/// part of booking creation for a one-off booking either - a new booking is
/// <see cref="BookingStatus.PaymentPending"/>, and
/// <c>ProviderAutoAssignmentHandler</c> places a provider only once it
/// reaches <see cref="BookingStatus.AwaitingFulfilment"/> - so this is a
/// forecast made deliberately early, which is the entire reason the job runs
/// <see cref="RecurringBookingOptions.LeadTimeDays"/> ahead of the date
/// ("enough lead time to catch and surface a problem before the customer
/// expects the visit"). It records one of three booked outcomes:
///
/// <list type="bullet">
/// <item><see cref="RecurringBookingOccurrenceOutcome.Booked"/> - no standing
/// provider yet, or the standing provider can serve the date.</item>
/// <item><see cref="RecurringBookingOccurrenceOutcome.BookedProviderReassigned"/> -
/// the standing provider cannot, but a substitute can. The occurrence is
/// handed to the existing reassignment flow instead of being skipped;
/// <c>ProviderAutoAssignmentHandler</c> makes the swap for real, which raises
/// <c>BookingProviderChangedEvent</c> and tells the customer (task 295).</item>
/// <item><see cref="RecurringBookingOccurrenceOutcome.BookedProviderUnavailable"/> -
/// nobody is eligible. Recorded with its reason and logged as a warning, not
/// dropped; the booking joins the manual admin queue, exactly where an
/// unstaffable one-off booking already goes.</item>
/// </list>
///
/// The search for a substitute is <see cref="IEligibleProviderSearchService"/>
/// - the same ranking-plus-gate walk the auto-assignment engine performs, not
/// a second matcher with its own idea of who is available.
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
    private readonly IRecurringPlanProviderContinuityService _continuityService;
    private readonly IProviderAssignmentEligibilityService _eligibilityService;
    private readonly IEligibleProviderSearchService _eligibleProviderSearch;
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
        IRecurringPlanProviderContinuityService continuityService,
        IProviderAssignmentEligibilityService eligibilityService,
        IEligibleProviderSearchService eligibleProviderSearch,
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
        _continuityService = continuityService;
        _eligibilityService = eligibilityService;
        _eligibleProviderSearch = eligibleProviderSearch;
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

        // Task 297: the plan id goes in through the orchestration's own
        // parameter, so the occurrence is a plan booking from its very first
        // INSERT rather than a one-off that gets adopted a moment later.
        Result<BookingDetailResponse> result = await _bookingService.CreateAsync(plan.CustomerId, request, plan.Id);

        if (result.IsSuccess)
        {
            var (outcome, providerNote) = await ResolveProviderPlacementAsync(plan, result.Value.Id, occurrenceDate, cancellationToken);
            await RecordOccurrenceAsync(plan, occurrenceDate, outcome, result.Value.Id, providerNote, cancellationToken);
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

    /// <summary>
    /// Task 297's provider-unavailable-on-date handling, for a booking that
    /// has just been created. Returns the outcome to record and the
    /// human-readable note that goes with it (never a raw error or an
    /// internal detail - docs/CODING-STANDARDS.md).
    ///
    /// The decision is a forecast, not an assignment: nothing here writes a
    /// <c>BookingProviderAssignment</c>, because a brand new booking is
    /// PaymentPending and assignment belongs at
    /// <see cref="BookingStatus.AwaitingFulfilment"/> for a recurring
    /// occurrence exactly as it does for a one-off. What it buys is the thing
    /// the row asks for - the generator no longer treats "the regular
    /// professional can't make this date" as a reason to drop the visit, and
    /// the one case where nobody at all can serve it is recorded instead of
    /// disappearing.
    /// </summary>
    private async Task<(RecurringBookingOccurrenceOutcome Outcome, string? Note)> ResolveProviderPlacementAsync(
        RecurringBookingPlan plan,
        Guid bookingId,
        DateOnly occurrenceDate,
        CancellationToken cancellationToken)
    {
        var standingProviderId = await _continuityService.FindStandingProviderAsync(plan.Id, bookingId);
        if (standingProviderId is null)
        {
            // Nothing to keep continuous - the plan's first occurrence, or one
            // whose history carries no provider. Ordinary matching decides,
            // and there is nothing to tell the customer about.
            return (RecurringBookingOccurrenceOutcome.Booked, null);
        }

        if (await _eligibilityService.IsEligibleAsync(standingProviderId.Value, bookingId, cancellationToken))
        {
            return (RecurringBookingOccurrenceOutcome.Booked, null);
        }

        // The fall-back is the existing flow, streamed lazily so an eligible
        // substitute found first costs nothing for the rest of the ranked
        // list (each gate check can be a billed route lookup - task 289).
        await foreach (var substitute in _eligibleProviderSearch
            .FindEligibleAsync(bookingId, [standingProviderId.Value], cancellationToken))
        {
            _logger.LogInformation(
                "Recurring plan {PlanId}: standing provider {StandingProviderId} is unavailable on {ScheduledDate}; "
                + "provider {SubstituteProviderId} is eligible, so booking {BookingId} goes to reassignment rather than being skipped.",
                plan.Id, standingProviderId.Value, occurrenceDate, substitute.ProviderId, bookingId);

            return (
                RecurringBookingOccurrenceOutcome.BookedProviderReassigned,
                "The professional who usually handles this plan is unavailable on this date; another one will be assigned.");
        }

        _logger.LogWarning(
            "Recurring plan {PlanId}: no eligible provider at all for {ScheduledDate}; booking {BookingId} was still created and needs manual assignment.",
            plan.Id, occurrenceDate, bookingId);

        return (
            RecurringBookingOccurrenceOutcome.BookedProviderUnavailable,
            "No professional is currently available for this date; the visit is booked and will be assigned manually.");
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

        // Task 297: driven by "did a booking happen", not by a single enum
        // member. A BookedProviderReassigned/BookedProviderUnavailable
        // occurrence produced a real visit on the customer's calendar, so
        // telling them it was skipped would be a lie; the staffing detail is
        // an ops concern that lives in the occurrence log and the warning log
        // above, and the customer hears about a provider change from the
        // reassignment flow itself (task 295's ProviderChanged) at the moment
        // it actually happens, rather than from a forecast days earlier that
        // supply may yet make untrue.
        var eventType = outcome.CreatedBooking()
            ? NotificationEventType.RecurringBookingUpcoming
            : NotificationEventType.RecurringBookingSkipped;

        await _notificationDispatchService.DispatchAsync(
            plan.CustomerId, eventType, recipient, variables, bookingId: bookingId, cancellationToken: cancellationToken);
    }
}
