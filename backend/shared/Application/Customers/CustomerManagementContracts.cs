using Nestly.Domain;

namespace Nestly.Application.Customers;

/// <summary>
/// Search/filter criteria for the admin customer list (SRS 12.4.1). All
/// filters are optional and combine with AND; string filters are
/// case-insensitive substring matches.
/// </summary>
public sealed record CustomerSearchFilter(
    string? Name,
    string? Mobile,
    string? Email,
    string? City,
    CustomerStatus? Status,
    DateTime? RegisteredFromUtc,
    DateTime? RegisteredToUtc,
    int? MinBookingCount,
    int? MaxBookingCount,
    int Page,
    int PageSize);

/// <summary>One customer row from a search, with its denormalized booking count (SRS 12.4.1 "Booking count" filter/column).</summary>
public sealed record CustomerSearchRow(Customer Customer, int BookingCount);

/// <summary>A page of <see cref="CustomerSearchRow"/> plus the total match count, for pagination.</summary>
public sealed record CustomerSearchResult(IReadOnlyList<CustomerSearchRow> Rows, int TotalCount);

/// <summary>Query-string shape of a customer search request (task 101a).</summary>
public sealed record CustomerSearchRequest(
    string? Name,
    string? Mobile,
    string? Email,
    string? City,
    CustomerStatus? Status,
    DateTime? RegisteredFromUtc,
    DateTime? RegisteredToUtc,
    int? MinBookingCount,
    int? MaxBookingCount,
    int Page = 1,
    int PageSize = 20);

public sealed record CustomerSummaryResponse(
    Guid Id,
    string Name,
    string Mobile,
    string? Email,
    string? City,
    CustomerStatus Status,
    DateTime CreatedAtUtc,
    int BookingCount);

public sealed record CustomerSearchResponse(
    IReadOnlyList<CustomerSummaryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record CustomerAddressResponse(
    Guid Id,
    string Label,
    string Line1,
    string? Line2,
    string? Landmark,
    string Pincode,
    string City,
    string State,
    bool IsDefault);

/// <summary>One booking row in the 360 view's booking history (SRS 12.4.2).</summary>
public sealed record CustomerBookingResponse(
    Guid Id,
    BookingStatus Status,
    DateOnly SlotDate,
    decimal TotalPayableSnapshot,
    DateTime CreatedAtUtc);

/// <summary>One wallet ledger row in the 360 view (SRS 12.4.2 "Wallet/refund history").</summary>
public sealed record CustomerWalletEntryResponse(
    Guid Id,
    WalletEntryType EntryType,
    decimal Amount,
    decimal BalanceAfter,
    WalletSourceType SourceType,
    string Description,
    DateTime CreatedAtUtc);

/// <summary>One redeemed coupon in the 360 view (SRS 12.4.2 "Coupons used").</summary>
public sealed record CustomerCouponUsageResponse(
    Guid CouponId,
    string CouponCode,
    Guid BookingId,
    decimal DiscountAmount,
    DateTime RedeemedAtUtc);

/// <summary>One support ticket in the 360 view (SRS 12.4.2 "Support tickets").</summary>
public sealed record CustomerSupportTicketResponse(
    Guid Id,
    SupportTicketCategory Category,
    SupportTicketPriority Priority,
    string Subject,
    SupportTicketStatus Status,
    DateTime CreatedAtUtc);

public sealed record CustomerNoteResponse(
    Guid Id,
    Guid AuthorAdminUserId,
    string Note,
    DateTime CreatedAtUtc);

/// <summary>The full customer 360 view (SRS 12.4.2, task 101b): profile, addresses, bookings, wallet, coupons, tickets and notes in one payload.</summary>
public sealed record CustomerDetailResponse(
    Guid Id,
    string Name,
    string Mobile,
    string? Email,
    DateTime? DateOfBirth,
    string? City,
    string? State,
    string? Pincode,
    string? Country,
    CustomerStatus Status,
    DateTime CreatedAtUtc,
    IReadOnlyList<CustomerAddressResponse> Addresses,
    IReadOnlyList<CustomerBookingResponse> Bookings,
    decimal WalletBalance,
    IReadOnlyList<CustomerWalletEntryResponse> WalletEntries,
    IReadOnlyList<CustomerCouponUsageResponse> Coupons,
    IReadOnlyList<CustomerSupportTicketResponse> SupportTickets,
    IReadOnlyList<CustomerNoteResponse> Notes);

/// <summary>Block a customer's account (SRS 12.4.3, task 101c). A reason is required for the audit trail.</summary>
public sealed record BlockCustomerRequest(string Reason);

/// <summary>Add an internal note to a customer's record (SRS 12.4.3, task 101d).</summary>
public sealed record AddCustomerNoteRequest(string Note);
