using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A customer's standing instruction to repeat a booking on a schedule
/// (PRODUCT-ENHANCEMENTS.md section 2, tasks 184-188) - e.g. "clean my flat
/// every other Tuesday morning". This entity only ever describes the
/// schedule; it never creates a booking itself. The Hangfire scheduler
/// (task 185, <c>IRecurringBookingSchedulerService</c>) reads plans that are
/// due and calls the exact same booking-creation orchestration a customer's
/// own "Book now" tap uses (<c>IBookingService.CreateAsync</c>, task 58) -
/// this aggregate is deliberately thin and holds no pricing, payment, or
/// serviceability logic of its own, so there is no second copy of those
/// rules to keep in sync with Booking's.
///
/// Unlike <see cref="Booking"/>, <see cref="AddressId"/> and
/// <see cref="SlotWindowId"/> here are live references, not snapshots - a
/// plan re-resolves the customer's current address and the current slot
/// window definition at every occurrence (the orchestration re-validates
/// both fresh each time), because a standing instruction should track "my
/// home" and "Tuesday mornings", not freeze whatever the address/window
/// looked like on the day the plan was created. If the address is later
/// deleted or the window deactivated, the next occurrence simply fails
/// orchestration validation and is skipped-and-notified like any other
/// unavailable-slot case.
///
/// OPEN DECISION (closed, see PRODUCT-ENHANCEMENTS.md OPEN DECISIONS):
/// a skipped occurrence (slot unavailable) does not count against
/// <see cref="OccurrenceCount"/> - only successfully booked occurrences
/// increment <see cref="CompletedOccurrenceCount"/>. See
/// <see cref="RecordOccurrenceSkipped"/>.
/// </summary>
public class RecurringBookingPlan : AggregateRoot<Guid>
{
    private readonly List<RecurringBookingPlanAddOn> _addOns = [];

    public Guid CustomerId { get; private set; }

    public Guid ServiceId { get; private set; }

    public Guid CityId { get; private set; }

    public Guid LocalityId { get; private set; }

    /// <summary>Live reference - see the class doc comment for why this differs from Booking's snapshot fields.</summary>
    public Guid AddressId { get; private set; }

    /// <summary>Live reference - the recurring time-of-day window (e.g. "9am-11am"). Re-validated by the orchestration at every occurrence.</summary>
    public Guid SlotWindowId { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>
    /// Whether every occurrence this plan generates should apply the
    /// customer's wallet balance (task 370). Chosen once, at plan creation,
    /// and reused for every future occurrence - there is no per-occurrence
    /// UI moment to ask again, unlike an ad-hoc booking's own wallet
    /// checkbox (<c>BookingSummaryRequest.ApplyWalletCredit</c>). Defaults
    /// to false, matching that checkbox's own off-by-default precedent
    /// (booking/summary's UI deliberately avoids a silent auto-apply).
    /// </summary>
    public bool ApplyWalletCredit { get; private set; }

    public RecurringBookingRecurrenceFrequency Frequency { get; private set; }

    /// <summary>Required for <see cref="RecurringBookingRecurrenceFrequency.Weekly"/>/<see cref="RecurringBookingRecurrenceFrequency.Biweekly"/>; null for <see cref="RecurringBookingRecurrenceFrequency.Monthly"/>.</summary>
    public DayOfWeek? RecurrenceDayOfWeek { get; private set; }

    /// <summary>Required for <see cref="RecurringBookingRecurrenceFrequency.Monthly"/> (1-31, clamped to the actual month length); null otherwise.</summary>
    public int? RecurrenceDayOfMonth { get; private set; }

    public DateOnly StartDate { get; private set; }

    /// <summary>At least one of <see cref="EndDate"/>/<see cref="OccurrenceCount"/> must be set - an unbounded plan would schedule forever with nothing to ever mark it <see cref="RecurringBookingPlanStatus.Completed"/>.</summary>
    public DateOnly? EndDate { get; private set; }

    public int? OccurrenceCount { get; private set; }

    /// <summary>Successfully booked occurrences only - a skipped occurrence never increments this. See the class doc comment's OPEN DECISION note.</summary>
    public int CompletedOccurrenceCount { get; private set; }

    /// <summary>The next date the scheduler should attempt (or skip-and-notify), advanced by <see cref="RecordOccurrenceBooked"/>/<see cref="RecordOccurrenceSkipped"/> regardless of outcome.</summary>
    public DateOnly NextOccurrenceDate { get; private set; }

    public RecurringBookingPlanStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyList<RecurringBookingPlanAddOn> AddOns => _addOns;

    protected RecurringBookingPlan() { }

    public RecurringBookingPlan(
        Guid id,
        Guid customerId,
        Guid serviceId,
        Guid cityId,
        Guid localityId,
        Guid addressId,
        Guid slotWindowId,
        int quantity,
        RecurringBookingRecurrenceFrequency frequency,
        DayOfWeek? recurrenceDayOfWeek,
        int? recurrenceDayOfMonth,
        DateOnly startDate,
        DateOnly? endDate,
        int? occurrenceCount,
        IReadOnlyList<(Guid AddOnId, int Quantity)>? addOns = null,
        bool applyWalletCredit = false)
        : base(id)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        ValidateRecurrenceFields(frequency, recurrenceDayOfWeek, recurrenceDayOfMonth);

        if (endDate is null && occurrenceCount is null)
        {
            throw new ArgumentException("A recurring plan must be bounded by an end date, an occurrence count, or both.");
        }

        if (endDate is { } end && end < startDate)
        {
            throw new ArgumentOutOfRangeException(nameof(endDate), "End date cannot be before the start date.");
        }

        if (occurrenceCount is { } count && count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(occurrenceCount), "Occurrence count must be positive.");
        }

        CustomerId = customerId;
        ServiceId = serviceId;
        CityId = cityId;
        LocalityId = localityId;
        AddressId = addressId;
        SlotWindowId = slotWindowId;
        Quantity = quantity;
        ApplyWalletCredit = applyWalletCredit;
        Frequency = frequency;
        RecurrenceDayOfWeek = recurrenceDayOfWeek;
        RecurrenceDayOfMonth = recurrenceDayOfMonth;
        StartDate = startDate;
        EndDate = endDate;
        OccurrenceCount = occurrenceCount;
        CompletedOccurrenceCount = 0;
        Status = RecurringBookingPlanStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;

        NextOccurrenceDate = NextOccurrenceOnOrAfter(startDate, frequency, recurrenceDayOfWeek, recurrenceDayOfMonth);

        foreach (var (addOnId, addOnQuantity) in addOns ?? [])
        {
            _addOns.Add(new RecurringBookingPlanAddOn(Guid.NewGuid(), Id, addOnId, addOnQuantity));
        }
    }

    /// <summary>
    /// Projects the next <paramref name="count"/> scheduled dates from
    /// <see cref="NextOccurrenceDate"/> forward, without persisting anything -
    /// pure calculation for the "list upcoming occurrences" API (task 186).
    /// Stops early at <see cref="EndDate"/> or once <see cref="OccurrenceCount"/>
    /// worth of bookings would be reached (assuming, optimistically, every
    /// remaining projected date is eventually booked rather than skipped -
    /// this is a preview, not a guarantee, exactly like <c>RevalidateSlotAsync</c>
    /// is a preview rather than a hold).
    /// </summary>
    public IReadOnlyList<DateOnly> PreviewUpcomingOccurrenceDates(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        var dates = new List<DateOnly>(count);
        var candidate = NextOccurrenceDate;
        int remainingBudget = OccurrenceCount is { } target ? Math.Max(0, target - CompletedOccurrenceCount) : int.MaxValue;

        while (dates.Count < count && dates.Count < remainingBudget)
        {
            if (EndDate is { } end && candidate > end)
            {
                break;
            }

            dates.Add(candidate);
            candidate = NextOccurrenceStrictlyAfter(candidate, Frequency, RecurrenceDayOfWeek, RecurrenceDayOfMonth);
        }

        return dates;
    }

    /// <summary>Active -> Paused. The scheduler skips a paused plan entirely (never even attempts, never skip-and-notifies) - pausing is the customer saying "not right now", not "keep trying and tell me".</summary>
    public void Pause()
    {
        if (Status != RecurringBookingPlanStatus.Active)
        {
            throw new InvalidOperationException($"Only an active plan can be paused (current status: {Status}).");
        }

        Status = RecurringBookingPlanStatus.Paused;
    }

    /// <summary>
    /// Paused -> Active. Not in PRODUCT-ENHANCEMENTS.md's literal API list
    /// ("create/pause/cancel"), added because pause is meaningless as a
    /// customer-facing action without a way back - without resume, "pause"
    /// would just be a slower, more confusing "cancel". <see cref="NextOccurrenceDate"/>
    /// is left exactly where it was; a plan paused mid-cycle resumes from
    /// the same next date rather than fast-forwarding, so the customer never
    /// loses a date they were still owed.
    /// </summary>
    public void Resume()
    {
        if (Status != RecurringBookingPlanStatus.Paused)
        {
            throw new InvalidOperationException($"Only a paused plan can be resumed (current status: {Status}).");
        }

        Status = RecurringBookingPlanStatus.Active;
    }

    /// <summary>Active or Paused -> Cancelled. Terminal - a cancelled plan can never be resumed (create a new one instead), same one-way-door convention <c>BookingLifecycle</c> uses for its own terminal states.</summary>
    public void Cancel()
    {
        if (Status is RecurringBookingPlanStatus.Cancelled or RecurringBookingPlanStatus.Completed)
        {
            throw new InvalidOperationException($"A {Status} plan cannot be cancelled.");
        }

        Status = RecurringBookingPlanStatus.Cancelled;
    }

    /// <summary>
    /// Called by the scheduler after the orchestration successfully created a
    /// booking for <paramref name="occurrenceDate"/>. Advances the schedule
    /// and completes the plan once its occurrence budget is exhausted or its
    /// end date has passed.
    /// </summary>
    public void RecordOccurrenceBooked(DateOnly occurrenceDate)
    {
        EnsureIsDueDate(occurrenceDate);

        CompletedOccurrenceCount++;
        AdvanceOrComplete();
    }

    /// <summary>
    /// Called by the scheduler when the occurrence was skipped (slot no
    /// longer available, or the orchestration otherwise rejected the
    /// attempt). Deliberately does not touch <see cref="CompletedOccurrenceCount"/>
    /// - see the class doc comment's OPEN DECISION note: a supply-side miss
    /// is not charged against the customer's occurrence budget, so the plan
    /// effectively extends by one date rather than delivering one fewer
    /// visit than promised.
    /// </summary>
    public void RecordOccurrenceSkipped(DateOnly occurrenceDate)
    {
        EnsureIsDueDate(occurrenceDate);

        AdvanceOrComplete();
    }

    private void EnsureIsDueDate(DateOnly occurrenceDate)
    {
        if (Status != RecurringBookingPlanStatus.Active)
        {
            throw new InvalidOperationException($"Cannot record an occurrence for a plan that is not active (current status: {Status}).");
        }

        if (occurrenceDate != NextOccurrenceDate)
        {
            throw new InvalidOperationException(
                $"Occurrence date {occurrenceDate:yyyy-MM-dd} does not match this plan's next due date {NextOccurrenceDate:yyyy-MM-dd}.");
        }
    }

    private void AdvanceOrComplete()
    {
        var next = NextOccurrenceStrictlyAfter(NextOccurrenceDate, Frequency, RecurrenceDayOfWeek, RecurrenceDayOfMonth);

        bool occurrenceBudgetExhausted = OccurrenceCount is { } target && CompletedOccurrenceCount >= target;
        bool pastEndDate = EndDate is { } end && next > end;

        if (occurrenceBudgetExhausted || pastEndDate)
        {
            Status = RecurringBookingPlanStatus.Completed;
        }

        NextOccurrenceDate = next;
    }

    private static void ValidateRecurrenceFields(RecurringBookingRecurrenceFrequency frequency, DayOfWeek? dayOfWeek, int? dayOfMonth)
    {
        switch (frequency)
        {
            case RecurringBookingRecurrenceFrequency.Weekly:
            case RecurringBookingRecurrenceFrequency.Biweekly:
                if (dayOfWeek is null)
                {
                    throw new ArgumentException("A day of week is required for a weekly or biweekly plan.", nameof(dayOfWeek));
                }

                if (dayOfMonth is not null)
                {
                    throw new ArgumentException("A day of month must not be set for a weekly or biweekly plan.", nameof(dayOfMonth));
                }

                break;

            case RecurringBookingRecurrenceFrequency.Monthly:
                if (dayOfMonth is null || dayOfMonth is < 1 or > 31)
                {
                    throw new ArgumentException("A day of month between 1 and 31 is required for a monthly plan.", nameof(dayOfMonth));
                }

                if (dayOfWeek is not null)
                {
                    throw new ArgumentException("A day of week must not be set for a monthly plan.", nameof(dayOfWeek));
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(frequency));
        }
    }

    private static DateOnly NextOccurrenceOnOrAfter(
        DateOnly from, RecurringBookingRecurrenceFrequency frequency, DayOfWeek? dayOfWeek, int? dayOfMonth)
    {
        if (frequency is RecurringBookingRecurrenceFrequency.Weekly or RecurringBookingRecurrenceFrequency.Biweekly)
        {
            var candidate = from;
            while (candidate.DayOfWeek != dayOfWeek!.Value)
            {
                candidate = candidate.AddDays(1);
            }

            return candidate;
        }

        var sameMonth = ClampToMonth(from.Year, from.Month, dayOfMonth!.Value);
        if (sameMonth >= from)
        {
            return sameMonth;
        }

        var nextMonth = from.AddDays(1 - from.Day).AddMonths(1);
        return ClampToMonth(nextMonth.Year, nextMonth.Month, dayOfMonth.Value);
    }

    private static DateOnly NextOccurrenceStrictlyAfter(
        DateOnly current, RecurringBookingRecurrenceFrequency frequency, DayOfWeek? dayOfWeek, int? dayOfMonth)
    {
        return frequency switch
        {
            RecurringBookingRecurrenceFrequency.Weekly => current.AddDays(7),
            RecurringBookingRecurrenceFrequency.Biweekly => current.AddDays(14),
            RecurringBookingRecurrenceFrequency.Monthly => NextMonthOccurrence(current, dayOfMonth!.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency)),
        };
    }

    private static DateOnly NextMonthOccurrence(DateOnly current, int dayOfMonth)
    {
        var firstOfNextMonth = current.AddDays(1 - current.Day).AddMonths(1);
        return ClampToMonth(firstOfNextMonth.Year, firstOfNextMonth.Month, dayOfMonth);
    }

    private static DateOnly ClampToMonth(int year, int month, int day) =>
        new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
}
