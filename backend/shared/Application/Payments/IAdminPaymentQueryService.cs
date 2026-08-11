using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Payments;

/// <summary>
/// Read side of the admin payment transaction view (SRS 12.13.1, task 311).
/// See <see cref="AdminPaymentTransactionFilterRequest"/>'s doc comment for
/// how this relates to the customer-facing and booking-detail-embedded
/// payment views that already existed before this.
/// </summary>
public interface IAdminPaymentQueryService
{
    Task<Result<PagedAdminPaymentTransactionResponse>> SearchAsync(AdminPaymentTransactionFilterRequest filter);

    /// <summary>Full detail for one transaction, including its attempts and any refunds raised against it.</summary>
    Task<Result<AdminPaymentTransactionDetailResponse>> GetDetailAsync(Guid transactionId);
}
