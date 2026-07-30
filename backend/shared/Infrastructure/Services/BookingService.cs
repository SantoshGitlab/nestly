using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Booking creation and reads (SRS 13, tasks 58-61).</summary>
public class BookingService : IBookingService
{
    /// <summary>Recorded on the auto-transition to PaymentPending, since there is no real payment gateway integration yet to explain it instead (Phase 4).</summary>
    private const string NoPaymentGatewayReason = "No payment gateway integrated yet - booking moves directly to awaiting payment.";

    private readonly IBookingSummaryService _summaryService;
    private readonly IBookingRepository _bookingRepository;
    private readonly ICustomerRepository _customerRepository;

    public BookingService(IBookingSummaryService summaryService, IBookingRepository bookingRepository, ICustomerRepository customerRepository)
    {
        _summaryService = summaryService;
        _bookingRepository = bookingRepository;
        _customerRepository = customerRepository;
    }

    public async Task<Result<BookingDetailResponse>> CreateAsync(Guid customerId, BookingSummaryRequest request)
    {
        // Re-validates every precondition (58a-f) through the same code path
        // the preview uses, so creation can never succeed on a combination
        // the preview would have rejected.
        var summaryResult = await _summaryService.GetSummaryAsync(customerId, request);
        if (summaryResult.IsFailure)
        {
            return summaryResult.Error;
        }

        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null)
        {
            return Error.NotFound("Booking.CustomerNotFound", "The specified customer does not exist.");
        }

        var summary = summaryResult.Value;

        var booking = new Booking(
            Guid.NewGuid(),
            customerId,
            new CustomerSnapshot(customer.Name, customer.Mobile),
            summary.Address.Id,
            new AddressSnapshot(
                summary.Address.Label, summary.Address.Line1, summary.Address.Line2, summary.Address.Landmark,
                summary.Address.Pincode, summary.Address.City, summary.Address.State,
                summary.Address.Latitude, summary.Address.Longitude,
                summary.Address.ContactName, summary.Address.ContactMobile),
            new SlotSnapshot(summary.Slot.SlotWindowId, summary.Slot.Date, summary.Slot.Name, summary.Slot.StartTime, summary.Slot.EndTime),
            new PriceSnapshot(
                summary.Price.BasePrice, summary.Price.Quantity, summary.Price.BaseTotal, summary.Price.AddOnTotal,
                summary.Price.VisitCharge, summary.Price.Subtotal, summary.Price.TaxPercentage,
                summary.Price.TaxAmount, summary.Price.PlatformFee, summary.Price.TotalPayable));

        // Add-on line items come from the price breakdown, not summary.AddOns:
        // the breakdown already carries each selection's quantity and
        // resolved unit price, exactly what the snapshot needs, whereas
        // summary.AddOns is a plain catalog projection for display.
        var item = booking.AddItem(
            Guid.NewGuid(), summary.Service.Id, summary.Service.Name, summary.Service.Slug,
            summary.Price.BasePrice, summary.Price.Quantity);

        foreach (var addOnLine in summary.Price.AddOnLineItems)
        {
            item.AddAddOn(Guid.NewGuid(), addOnLine.AddOnId, addOnLine.Name, addOnLine.UnitPrice, addOnLine.Quantity);
        }

        booking.TransitionTo(BookingStatus.PaymentPending, NoPaymentGatewayReason);

        await _bookingRepository.AddAsync(booking);

        return Result.Success(ToDetailResponse(booking));
    }

    public async Task<Result<IReadOnlyList<BookingListItemResponse>>> ListAsync(Guid customerId, BookingStatusBucket? bucket)
    {
        var statuses = bucket is null
            ? Enum.GetValues<BookingStatus>()
            : BookingStatusMapper.StatusesInBucket(bucket.Value);

        var bookings = await _bookingRepository.ListByCustomerAsync(customerId, statuses);

        IReadOnlyList<BookingListItemResponse> response = bookings.Select(ToListItem).ToList();
        return Result.Success(response);
    }

    public async Task<Result<BookingDetailResponse>> GetDetailAsync(Guid customerId, Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null || booking.CustomerId != customerId)
        {
            return Error.NotFound("Booking.NotFound", "The specified booking does not exist.");
        }

        return Result.Success(ToDetailResponse(booking));
    }

    private static BookingListItemResponse ToListItem(Booking booking) => new(
        booking.Id,
        booking.Items.Count > 0 ? booking.Items[0].NameSnapshot : string.Empty,
        booking.SlotDate,
        booking.TotalPayableSnapshot,
        booking.Status,
        BookingStatusMapper.LabelFor(booking.Status),
        booking.CreatedAtUtc);

    private static BookingDetailResponse ToDetailResponse(Booking booking)
    {
        var item = booking.Items.Count > 0 ? booking.Items[0] : null;

        var addOns = item?.AddOns
            .Select(a => new Application.Catalog.ServiceAddOnSummaryResponse(a.ServiceAddOnId, a.NameSnapshot, null, a.UnitPriceSnapshot))
            .ToList()
            ?? [];

        return new BookingDetailResponse(
            booking.Id,
            new BookingServiceSummary(item?.ServiceId ?? Guid.Empty, item?.NameSnapshot ?? string.Empty, item?.SlugSnapshot ?? string.Empty),
            addOns,
            new BookingAddressSummary(
                booking.SourceAddressId ?? Guid.Empty, booking.AddressLabelSnapshot, booking.AddressLine1Snapshot,
                booking.AddressLine2Snapshot, booking.AddressLandmarkSnapshot, booking.AddressPincodeSnapshot,
                booking.AddressCitySnapshot, booking.AddressStateSnapshot, booking.AddressLatitudeSnapshot,
                booking.AddressLongitudeSnapshot, booking.AddressContactNameSnapshot, booking.AddressContactMobileSnapshot),
            new BookingSlotSummary(booking.SlotWindowId, booking.SlotWindowNameSnapshot, booking.SlotDate, booking.SlotStartTimeSnapshot, booking.SlotEndTimeSnapshot),
            new Application.Pricing.PriceBreakdownResponse(
                booking.BasePriceSnapshot, booking.QuantitySnapshot, booking.BaseTotalSnapshot,
                item?.AddOns.Select(a => new Application.Pricing.AddOnLineItem(a.ServiceAddOnId, a.NameSnapshot, a.UnitPriceSnapshot, a.Quantity, a.LineTotalSnapshot)).ToList() ?? [],
                booking.AddOnTotalSnapshot, booking.VisitChargeSnapshot, booking.SubtotalSnapshot,
                booking.TaxPercentageSnapshot, booking.TaxAmountSnapshot, booking.PlatformFeeSnapshot, booking.TotalPayableSnapshot),
            booking.Status,
            BookingStatusMapper.LabelFor(booking.Status),
            booking.StatusHistory
                .OrderBy(h => h.ChangedAtUtc)
                .Select(h => new BookingStatusTimelineEntry(h.FromStatus, h.ToStatus, BookingStatusMapper.LabelFor(h.ToStatus), h.Reason, h.ChangedAtUtc))
                .ToList(),
            booking.CreatedAtUtc);
    }
}
