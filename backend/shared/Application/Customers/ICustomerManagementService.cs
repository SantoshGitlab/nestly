using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Customers;

/// <summary>
/// Admin customer management (SRS 12.4, tasks 101a-101d): search/filter, the
/// 360 detail view, block/unblock, and internal notes. One service rather
/// than four, since every operation shares the same "load the customer,
/// fail with NotFound if missing" shape and the same set of collaborating
/// repositories (bookings, wallet, coupons, support tickets, notes).
/// </summary>
public interface ICustomerManagementService
{
    Task<Result<CustomerSearchResponse>> SearchAsync(CustomerSearchRequest request);

    Task<Result<CustomerDetailResponse>> GetDetailAsync(Guid customerId);

    /// <summary>Blocks a customer's account (SRS 12.4.3). <paramref name="adminUserId"/> is the acting admin, for audit.</summary>
    Task<Result<CustomerDetailResponse>> BlockAsync(Guid customerId, Guid adminUserId, string reason);

    /// <summary>Restores a blocked customer's account to Active (SRS 12.4.3).</summary>
    Task<Result<CustomerDetailResponse>> UnblockAsync(Guid customerId, Guid adminUserId);

    Task<Result<CustomerNoteResponse>> AddNoteAsync(Guid customerId, Guid adminUserId, string note);
}
