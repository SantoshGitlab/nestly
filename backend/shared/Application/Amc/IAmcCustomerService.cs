using Nestly.Application.Bookings;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Amc;

/// <summary>
/// Customer-facing AMC operations (docs/AMC.md): browse the plan catalog,
/// purchase a contract for a named asset, view "my contracts", and redeem
/// entitlement against one.
/// </summary>
public interface IAmcCustomerService
{
    Task<IReadOnlyList<AmcPlanBrowseResponse>> BrowsePlansAsync(Guid? categoryId = null);

    /// <summary>
    /// Purchases a plan (docs/AMC.md "HOW IT WORKS"): creates the contract.
    /// Does NOT charge a real payment gateway order - see docs/AMC.md OPEN
    /// DECISIONS: <see cref="Nestly.Domain.PaymentTransaction.BookingId"/> is
    /// a required FK and the whole gateway/webhook/commission/escrow
    /// pipeline assumes every transaction belongs to a booking, so wiring a
    /// real charge here is real follow-up work, not something to fake with a
    /// booking-shaped transaction. <see cref="Nestly.Domain.CustomerAmcContract.PaymentTransactionId"/>
    /// is left null for now; this is a stated MVP limitation, not an
    /// oversight.
    /// </summary>
    Task<Result<MyAmcContractResponse>> PurchaseAsync(Guid customerId, AmcContractPurchaseRequest request);

    Task<Result<IReadOnlyList<MyAmcContractResponse>>> ListMyContractsAsync(Guid customerId);

    Task<Result<MyAmcContractResponse>> GetMyContractAsync(Guid customerId, Guid contractId);

    Task<Result> CancelAsync(Guid customerId, Guid contractId);

    /// <summary>
    /// Redeems entitlement against a contract: creates an ordinary booking
    /// through the SAME <see cref="IBookingService.CreateAsync"/> orchestration
    /// a normal "Book now" tap uses (address, slot, service selection all
    /// still validated), zero-priced, linked back to the contract. The
    /// contract's <see cref="Nestly.Domain.CustomerAmcContract.VisitsRemaining"/>
    /// is NOT decremented here - see docs/AMC.md's "on completion, not
    /// creation" rule; that happens when the resulting booking reaches
    /// Completed, via <c>AmcVisitOnBookingCompletionHandler</c>.
    /// </summary>
    Task<Result<BookingDetailResponse>> RedeemVisitAsync(Guid customerId, Guid contractId, BookingSummaryRequest request);
}
