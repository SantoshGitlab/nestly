using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Application.Coupons;
using Nestly.Application.Customers;
using Nestly.Application.Support;
using Nestly.Application.Wallet;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Admin customer management (SRS 12.4, tasks 101a-101d). Composes the
/// existing Booking/Wallet/Coupon/SupportTicket repositories rather than
/// introducing new tables for data that already lives in those modules -
/// only <see cref="CustomerNote"/> (task 101d) is genuinely new.
/// </summary>
public class CustomerManagementService : ICustomerManagementService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerAddressRepository _addressRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IWalletLedgerRepository _walletLedgerRepository;
    private readonly ICouponRedemptionRepository _couponRedemptionRepository;
    private readonly ICouponRepository _couponRepository;
    private readonly ISupportTicketRepository _supportTicketRepository;
    private readonly ICustomerNoteRepository _noteRepository;

    private static readonly IReadOnlyList<BookingStatus> AllBookingStatuses = Enum.GetValues<BookingStatus>();

    public CustomerManagementService(
        ICustomerRepository customerRepository,
        ICustomerAddressRepository addressRepository,
        IBookingRepository bookingRepository,
        IWalletLedgerRepository walletLedgerRepository,
        ICouponRedemptionRepository couponRedemptionRepository,
        ICouponRepository couponRepository,
        ISupportTicketRepository supportTicketRepository,
        ICustomerNoteRepository noteRepository)
    {
        _customerRepository = customerRepository;
        _addressRepository = addressRepository;
        _bookingRepository = bookingRepository;
        _walletLedgerRepository = walletLedgerRepository;
        _couponRedemptionRepository = couponRedemptionRepository;
        _couponRepository = couponRepository;
        _supportTicketRepository = supportTicketRepository;
        _noteRepository = noteRepository;
    }

    public async Task<Result<CustomerSearchResponse>> SearchAsync(CustomerSearchRequest request)
    {
        var filter = new CustomerSearchFilter(
            request.Name,
            request.Mobile,
            request.Email,
            request.City,
            request.Status,
            request.RegisteredFromUtc,
            request.RegisteredToUtc,
            request.MinBookingCount,
            request.MaxBookingCount,
            request.Page,
            request.PageSize);

        var result = await _customerRepository.SearchAsync(filter);

        var items = result.Rows
            .Select(row => new CustomerSummaryResponse(
                row.Customer.Id,
                row.Customer.Name,
                row.Customer.Mobile,
                row.Customer.Email,
                row.Customer.City,
                row.Customer.Status,
                row.Customer.CreatedAt,
                row.BookingCount))
            .ToList();

        return new CustomerSearchResponse(items, result.TotalCount, request.Page, request.PageSize);
    }

    public async Task<Result<CustomerDetailResponse>> GetDetailAsync(Guid customerId)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null)
        {
            return Error.NotFound("Customer.NotFound", "Customer was not found.");
        }

        return await BuildDetailAsync(customer);
    }

    public async Task<Result<CustomerDetailResponse>> BlockAsync(Guid customerId, Guid adminUserId, string reason)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null)
        {
            return Error.NotFound("Customer.NotFound", "Customer was not found.");
        }

        if (customer.Status == CustomerStatus.SoftDeleted)
        {
            return Error.Business("Customer.CannotBlockDeleted", "A deleted customer account cannot be blocked.");
        }

        if (customer.Status != CustomerStatus.Blocked)
        {
            customer.UpdateStatus(CustomerStatus.Blocked);
            await _customerRepository.UpdateAsync(customer);
            await _noteRepository.AddAsync(new CustomerNote(Guid.NewGuid(), customerId, adminUserId, $"Account blocked. Reason: {reason}"));
        }

        return await BuildDetailAsync(customer);
    }

    public async Task<Result<CustomerDetailResponse>> UnblockAsync(Guid customerId, Guid adminUserId)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null)
        {
            return Error.NotFound("Customer.NotFound", "Customer was not found.");
        }

        if (customer.Status != CustomerStatus.Blocked)
        {
            return Error.Business("Customer.NotBlocked", "This customer's account is not currently blocked.");
        }

        customer.UpdateStatus(CustomerStatus.Active);
        await _customerRepository.UpdateAsync(customer);
        await _noteRepository.AddAsync(new CustomerNote(Guid.NewGuid(), customerId, adminUserId, "Account unblocked."));

        return await BuildDetailAsync(customer);
    }

    public async Task<Result<CustomerNoteResponse>> AddNoteAsync(Guid customerId, Guid adminUserId, string note)
    {
        if (!await _customerRepository.ExistsAsync(customerId))
        {
            return Error.NotFound("Customer.NotFound", "Customer was not found.");
        }

        var entry = new CustomerNote(Guid.NewGuid(), customerId, adminUserId, note);
        await _noteRepository.AddAsync(entry);

        return new CustomerNoteResponse(entry.Id, entry.AuthorAdminUserId, entry.Note, entry.CreatedAtUtc);
    }

    private async Task<CustomerDetailResponse> BuildDetailAsync(Customer customer)
    {
        var addresses = await _addressRepository.GetByCustomerAsync(customer.Id);
        var bookings = await _bookingRepository.ListByCustomerAsync(customer.Id, AllBookingStatuses);
        var walletEntries = await _walletLedgerRepository.ListByCustomerAsync(customer.Id);
        var redemptions = await _couponRedemptionRepository.ListByCustomerAsync(customer.Id);
        var supportTickets = await _supportTicketRepository.ListByCustomerAsync(customer.Id);
        var notes = await _noteRepository.ListByCustomerAsync(customer.Id);

        decimal walletBalance = walletEntries.Count > 0 ? walletEntries[0].BalanceAfter : 0m;

        // Small, bounded set per customer (usage caps keep redemption counts
        // low) - a cache avoids repeat lookups when the same coupon was used
        // more than once, without needing a new batch-fetch repository method.
        var couponCache = new Dictionary<Guid, Coupon?>();
        var coupons = new List<CustomerCouponUsageResponse>();
        foreach (var redemption in redemptions)
        {
            if (!couponCache.TryGetValue(redemption.CouponId, out var coupon))
            {
                coupon = await _couponRepository.GetByIdAsync(redemption.CouponId);
                couponCache[redemption.CouponId] = coupon;
            }

            coupons.Add(new CustomerCouponUsageResponse(
                redemption.CouponId,
                coupon?.Code ?? "(deleted coupon)",
                redemption.BookingId,
                redemption.DiscountAmount,
                redemption.RedeemedAtUtc));
        }

        return new CustomerDetailResponse(
            customer.Id,
            customer.Name,
            customer.Mobile,
            customer.Email,
            customer.DateOfBirth,
            customer.City,
            customer.State,
            customer.Pincode,
            customer.Country,
            customer.Status,
            customer.CreatedAt,
            addresses.Select(a => new CustomerAddressResponse(
                a.Id, a.Label, a.Line1, a.Line2, a.Landmark, a.Pincode, a.City, a.State, a.IsDefault)).ToList(),
            bookings.Select(b => new CustomerBookingResponse(
                b.Id, b.Status, b.SlotDate, b.TotalPayableSnapshot, b.CreatedAtUtc)).ToList(),
            walletBalance,
            walletEntries.Select(w => new CustomerWalletEntryResponse(
                w.Id, w.EntryType, w.Amount, w.BalanceAfter, w.SourceType, w.Description, w.CreatedAtUtc)).ToList(),
            coupons,
            supportTickets.Select(t => new CustomerSupportTicketResponse(
                t.Id, t.Category, t.Priority, t.Subject, t.Status, t.CreatedAtUtc)).ToList(),
            notes.Select(n => new CustomerNoteResponse(n.Id, n.AuthorAdminUserId, n.Note, n.CreatedAtUtc)).ToList());
    }
}
