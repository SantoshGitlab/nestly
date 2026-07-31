using System.Text.Json;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.BookingManagement;
using Nestly.Application.Bookings;
using Nestly.Application.Cancellations;
using Nestly.Application.Payments;
using Nestly.Application.Refunds;
using Nestly.Application.Reschedules;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Admin booking management (SRS 12.11, 12.13.2-3; tasks 115a-117c). Every
/// cancellation/reschedule/refund goes through the existing domain services
/// (<see cref="ICancellationService"/>, <see cref="IRescheduleService"/>,
/// <see cref="IRefundService"/> - tasks 80c, 82d, 75d), so their fee/
/// eligibility policy math is never duplicated here; this service is
/// responsible only for the admin-facing shape, the generic status
/// transition, and the audit trail (<see cref="IAuditLogWriter"/>).
/// </summary>
public class BookingManagementService : IBookingManagementService
{
    /// <summary>
    /// Target statuses that must go through their dedicated action instead of
    /// the generic <see cref="UpdateStatusAsync"/> - see
    /// <see cref="AdminBookingStatusUpdateRequest"/>'s doc comment.
    /// </summary>
    private static readonly IReadOnlyCollection<BookingStatus> DisallowedGenericTransitionTargets = new HashSet<BookingStatus>
    {
        BookingStatus.CancelledByCustomer,
        BookingStatus.CancelledByAdmin,
        BookingStatus.Rescheduled,
        BookingStatus.RefundPending,
        BookingStatus.Refunded,
    };

    private readonly IBookingRepository _bookingRepository;
    private readonly IPaymentTransactionRepository _paymentRepository;
    private readonly ICancellationRepository _cancellationRepository;
    private readonly IRescheduleRepository _rescheduleRepository;
    private readonly IRefundTransactionRepository _refundRepository;
    private readonly ICancellationService _cancellationService;
    private readonly IRescheduleService _rescheduleService;
    private readonly IRefundService _refundService;
    private readonly IAuditLogWriter _auditLogWriter;

    public BookingManagementService(
        IBookingRepository bookingRepository,
        IPaymentTransactionRepository paymentRepository,
        ICancellationRepository cancellationRepository,
        IRescheduleRepository rescheduleRepository,
        IRefundTransactionRepository refundRepository,
        ICancellationService cancellationService,
        IRescheduleService rescheduleService,
        IRefundService refundService,
        IAuditLogWriter auditLogWriter)
    {
        _bookingRepository = bookingRepository;
        _paymentRepository = paymentRepository;
        _cancellationRepository = cancellationRepository;
        _rescheduleRepository = rescheduleRepository;
        _refundRepository = refundRepository;
        _cancellationService = cancellationService;
        _rescheduleService = rescheduleService;
        _refundService = refundService;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<Result<AdminBookingSearchResponse>> SearchAsync(AdminBookingSearchRequest request)
    {
        var filter = new BookingSearchFilter(
            request.BookingId,
            request.CustomerName,
            request.CustomerMobile,
            request.Status,
            request.City,
            request.SlotDateFrom,
            request.SlotDateTo,
            request.CreatedFromUtc,
            request.CreatedToUtc,
            request.ServiceId,
            request.CategoryId,
            request.CouponCode,
            request.Page,
            request.PageSize);

        var result = await _bookingRepository.SearchAsync(filter);

        var items = result.Rows.Select(ToListItem).ToList();
        return new AdminBookingSearchResponse(items, result.TotalCount, request.Page, request.PageSize);
    }

    public async Task<Result<AdminBookingDetailResponse>> GetDetailAsync(Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return Error.NotFound("Booking.NotFound", "The specified booking does not exist.");
        }

        return await BuildDetailAsync(booking);
    }

    public async Task<Result<AdminBookingDetailResponse>> UpdateStatusAsync(Guid bookingId, Guid adminUserId, AdminBookingStatusUpdateRequest request)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return Error.NotFound("Booking.NotFound", "The specified booking does not exist.");
        }

        if (DisallowedGenericTransitionTargets.Contains(request.NewStatus))
        {
            return Error.Validation(
                "Booking.UseDedicatedAction",
                $"'{request.NewStatus}' must be set via the dedicated cancel/reschedule/refund action, not a generic status update.");
        }

        if (!BookingLifecycle.IsValidTransition(booking.Status, request.NewStatus))
        {
            return Error.Business(
                "Booking.InvalidTransition",
                $"A booking in status '{booking.Status}' cannot be moved to '{request.NewStatus}'.");
        }

        var previousStatus = booking.Status;
        booking.TransitionTo(request.NewStatus, request.Reason);
        await _bookingRepository.UpdateAsync(booking);

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "Booking", bookingId.ToString(), "AdminStatusUpdate",
            JsonSerializer.Serialize(new { Status = previousStatus.ToString() }),
            JsonSerializer.Serialize(new { Status = request.NewStatus.ToString(), request.Reason })));

        return await BuildDetailAsync(booking);
    }

    public async Task<Result<AdminBookingDetailResponse>> CancelAsync(Guid bookingId, Guid adminUserId, AdminCancelBookingRequest request)
    {
        var outcome = await _cancellationService.AdminCancelAsync(bookingId, request.Reason, request.InternalNotes);
        if (outcome.IsFailure)
        {
            return outcome.Error;
        }

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "Booking", bookingId.ToString(), "AdminCancel",
            null,
            JsonSerializer.Serialize(new
            {
                outcome.Value.BookingStatus,
                outcome.Value.CancellationFeeAmount,
                outcome.Value.RefundAmount,
                Reason = request.Reason
            })));

        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        return booking is null
            ? Error.NotFound("Booking.NotFound", "The specified booking does not exist.")
            : await BuildDetailAsync(booking);
    }

    public async Task<Result<AdminBookingDetailResponse>> RescheduleAsync(Guid bookingId, Guid adminUserId, AdminRescheduleBookingRequest request)
    {
        var rescheduleRequest = new RescheduleBookingRequest(request.LocalityId, request.SlotWindowId, request.SlotDate, request.Reason);
        var outcome = await _rescheduleService.AdminRescheduleAsync(bookingId, rescheduleRequest);
        if (outcome.IsFailure)
        {
            return outcome.Error;
        }

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "Booking", bookingId.ToString(), "AdminReschedule",
            JsonSerializer.Serialize(outcome.Value.PreviousSlot),
            JsonSerializer.Serialize(outcome.Value.NewSlot)));

        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        return booking is null
            ? Error.NotFound("Booking.NotFound", "The specified booking does not exist.")
            : await BuildDetailAsync(booking);
    }

    public async Task<Result<AdminBookingDetailResponse>> RefundAsync(Guid bookingId, Guid adminUserId, AdminRefundRequest request)
    {
        var refundResult = request.IsFullRefund
            ? await _refundService.InitiateFullRefundAsync(bookingId, request.Reason, request.Method)
            : await _refundService.InitiatePartialRefundAsync(bookingId, request.Amount!.Value, request.Reason, request.Method);

        if (refundResult.IsFailure)
        {
            return refundResult.Error;
        }

        // Every refund action is audited regardless of amount (SRS 12.13.2,
        // task 117c) - full and partial share one action name suffixed by
        // type so the audit-log-viewer's substring "Action" filter can still
        // isolate either.
        await _auditLogWriter.WriteAsync(new AuditEntry(
            "Booking", bookingId.ToString(), request.IsFullRefund ? "AdminRefundFull" : "AdminRefundPartial",
            null,
            JsonSerializer.Serialize(new
            {
                refundResult.Value.Id,
                refundResult.Value.Amount,
                refundResult.Value.Method,
                refundResult.Value.Status,
                Reason = request.Reason
            })));

        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        return booking is null
            ? Error.NotFound("Booking.NotFound", "The specified booking does not exist.")
            : await BuildDetailAsync(booking);
    }

    private async Task<AdminBookingDetailResponse> BuildDetailAsync(Booking booking)
    {
        var payment = await _paymentRepository.GetByBookingIdAsync(booking.Id);
        var cancellation = await _cancellationRepository.GetByBookingIdAsync(booking.Id);
        var reschedules = await _rescheduleRepository.ListByBookingAsync(booking.Id);
        var refunds = await _refundRepository.ListByBookingAsync(booking.Id);

        return new AdminBookingDetailResponse(
            booking.Id,
            new AdminBookingCustomerSnapshot(booking.CustomerId, booking.CustomerNameSnapshot, booking.CustomerMobileSnapshot),
            new AdminBookingAddressSnapshot(
                booking.AddressLabelSnapshot, booking.AddressLine1Snapshot, booking.AddressLine2Snapshot, booking.AddressLandmarkSnapshot,
                booking.AddressPincodeSnapshot, booking.AddressCitySnapshot, booking.AddressStateSnapshot,
                booking.AddressContactNameSnapshot, booking.AddressContactMobileSnapshot),
            new AdminBookingSlotSnapshot(booking.SlotWindowId, booking.SlotDate, booking.SlotWindowNameSnapshot, booking.SlotStartTimeSnapshot, booking.SlotEndTimeSnapshot),
            booking.Items.Select(i => new AdminBookingItemResponse(
                i.Id, i.ServiceId, i.NameSnapshot, i.UnitPriceSnapshot, i.Quantity, i.LineTotalSnapshot,
                i.AddOns.Select(a => new AdminBookingAddOnResponse(a.Id, a.ServiceAddOnId, a.NameSnapshot, a.UnitPriceSnapshot, a.Quantity, a.LineTotalSnapshot)).ToList())).ToList(),
            new AdminBookingPriceResponse(
                booking.BasePriceSnapshot, booking.QuantitySnapshot, booking.BaseTotalSnapshot, booking.AddOnTotalSnapshot,
                booking.VisitChargeSnapshot, booking.SubtotalSnapshot, booking.TaxPercentageSnapshot, booking.TaxAmountSnapshot,
                booking.PlatformFeeSnapshot, booking.TotalPayableSnapshot, booking.CouponCodeSnapshot, booking.CouponDiscountAmountSnapshot,
                booking.TotalPayableSnapshot),
            booking.Status,
            BookingStatusMapper.LabelFor(booking.Status),
            booking.StatusHistory
                .OrderBy(h => h.ChangedAtUtc)
                .Select(h => new AdminBookingStatusTimelineEntry(h.FromStatus, h.ToStatus, BookingStatusMapper.LabelFor(h.ToStatus), h.Reason, h.ChangedAtUtc))
                .ToList(),
            payment is null ? null : new AdminBookingPaymentSummary(
                payment.Id, payment.Status, payment.Amount, payment.Currency, payment.LatestAttempt?.GatewayPaymentRef, payment.CreatedAtUtc, payment.UpdatedAtUtc),
            cancellation is null ? null : new AdminBookingCancellationResponse(
                cancellation.Id, cancellation.Actor, cancellation.Reason, cancellation.WithinFreeCancellationWindow,
                cancellation.CancellationFeeAmount, cancellation.RefundAmount, cancellation.RefundMethod, cancellation.RefundTransactionId,
                cancellation.InternalNotes, cancellation.CreatedAtUtc),
            reschedules.Select(r => new AdminBookingRescheduleResponse(
                r.Id, r.Actor, r.Reason, r.FromSlotDate, r.FromSlotStartTime, r.ToSlotDate, r.ToSlotStartTime, r.IsLate, r.FeeAmount, r.CreatedAtUtc)).ToList(),
            refunds.Select(r => new AdminBookingRefundResponse(
                r.Id, r.Type, r.Method, r.Amount, r.Status, r.GatewayRefundRef, r.Reason, r.CreatedAtUtc, r.ProcessedAtUtc)).ToList(),
            booking.CreatedAtUtc);
    }

    private static AdminBookingListItemResponse ToListItem(Booking booking) => new(
        booking.Id,
        booking.CustomerNameSnapshot,
        booking.CustomerMobileSnapshot,
        booking.Items.Count > 0 ? booking.Items[0].NameSnapshot : "(no service)",
        booking.AddressCitySnapshot,
        booking.SlotDate,
        booking.Status,
        BookingStatusMapper.LabelFor(booking.Status),
        booking.TotalPayableSnapshot,
        booking.CouponCodeSnapshot,
        booking.CreatedAtUtc);
}
