using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.Catalog;
using Nestly.Application.Pricing;
using Nestly.Application.Slots;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Booking summary/preview (SRS 11.7, task 57). Composes the existing
/// slot-availability and price-calculation services rather than
/// re-implementing their validation - a summary must reject exactly the same
/// combinations booking creation (task 58) will reject, or a customer could
/// see a summary that then fails to book.
/// </summary>
public class BookingSummaryService : IBookingSummaryService
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceAddOnRepository _addOnRepository;
    private readonly ICustomerAddressRepository _addressRepository;
    private readonly ISlotAvailabilityService _slotAvailabilityService;
    private readonly IPriceCalculationService _priceCalculationService;

    public BookingSummaryService(
        IServiceRepository serviceRepository,
        IServiceAddOnRepository addOnRepository,
        ICustomerAddressRepository addressRepository,
        ISlotAvailabilityService slotAvailabilityService,
        IPriceCalculationService priceCalculationService)
    {
        _serviceRepository = serviceRepository;
        _addOnRepository = addOnRepository;
        _addressRepository = addressRepository;
        _slotAvailabilityService = slotAvailabilityService;
        _priceCalculationService = priceCalculationService;
    }

    public async Task<Result<BookingSummaryResponse>> GetSummaryAsync(Guid customerId, BookingSummaryRequest request)
    {
        if (request.Quantity <= 0)
        {
            return Error.Validation("Booking.InvalidQuantity", "Quantity must be positive.");
        }

        var service = await _serviceRepository.GetByIdAsync(request.ServiceId);
        if (service is null || !service.IsActive)
        {
            return Error.NotFound("Booking.ServiceNotFound", "The specified service does not exist.");
        }

        var address = await _addressRepository.GetByIdAsync(request.AddressId);
        if (address is null || address.CustomerId != customerId)
        {
            return Error.NotFound("Booking.AddressNotFound", "The specified address does not exist.");
        }

        var addOnSummaries = new List<ServiceAddOnSummaryResponse>(request.AddOns.Count);
        foreach (var selection in request.AddOns)
        {
            var addOn = await _addOnRepository.GetByIdAsync(selection.AddOnId);
            if (addOn is null || !addOn.IsActive || addOn.ServiceId != request.ServiceId)
            {
                return Error.Validation("Booking.InvalidAddOn", $"Add-on {selection.AddOnId} is not available for this service.");
            }

            addOnSummaries.Add(new ServiceAddOnSummaryResponse(addOn.Id, addOn.Name, addOn.Description, addOn.Price));
        }

        var availability = await _slotAvailabilityService.GetAvailableSlotsAsync(request.ServiceId, request.LocalityId, request.SlotDate);
        if (availability.IsFailure)
        {
            return availability.Error;
        }

        if (!availability.Value.IsServiceable)
        {
            return Error.Business("Booking.NotServiceable", "This service is not available at the selected address.");
        }

        var slot = availability.Value.Slots.FirstOrDefault(s => s.SlotWindowId == request.SlotWindowId);
        if (slot is null)
        {
            return Error.Business("Booking.SlotNotAvailable", "The selected slot is no longer available.");
        }

        var priceResult = await _priceCalculationService.CalculateAsync(
            new PriceCalculationRequest(request.ServiceId, request.CityId, request.Quantity, request.AddOns));
        if (priceResult.IsFailure)
        {
            return priceResult.Error;
        }

        var response = new BookingSummaryResponse(
            new BookingServiceSummary(service.Id, service.Name, service.Slug),
            addOnSummaries,
            new BookingAddressSummary(
                address.Id, address.Label, address.Line1, address.Line2, address.Landmark,
                address.Pincode, address.City, address.State, address.ContactName, address.ContactMobile),
            new BookingSlotSummary(slot.SlotWindowId, request.SlotDate, slot.StartTime, slot.EndTime),
            priceResult.Value,
            service.CancellationPolicy,
            service.ReschedulePolicy);

        return Result.Success(response);
    }
}
