using Nestly.Application.Bookings;
using Nestly.Application.Slots;
using Nestly.Domain;

namespace Nestly.Application.Reschedules;

/// <summary>Reschedule eligibility summary (SRS 11.15.1, tasks 82a-b).</summary>
public record RescheduleEligibilityResponse(
    bool IsEligible,
    string? IneligibilityReason,
    int ReschedulesUsed,
    int MaxReschedulesPerBooking,
    decimal MinHoursBeforeSlot);

/// <summary>Customer-submitted reschedule request (SRS 24.6). LocalityId re-anchors the slot lookup, mirroring how BookingSummaryRequest does it at booking time.</summary>
public record RescheduleBookingRequest(Guid LocalityId, Guid SlotWindowId, DateOnly SlotDate, string? Reason);

/// <summary>The outcome of a confirmed reschedule (SRS 11.15.2-3, 24.6).</summary>
public record RescheduleOutcomeResponse(
    Guid BookingId,
    BookingStatus BookingStatus,
    BookingSlotSummary PreviousSlot,
    BookingSlotSummary NewSlot,
    bool IsLate,
    decimal FeeAmount,
    int ReschedulesUsed,
    int MaxReschedulesPerBooking,
    DateTime RescheduledAtUtc);
