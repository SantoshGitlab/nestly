using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Application.PartnerManagement;

/// <summary>
/// Admin-triggered payout batch management (PARTNER.md Financial Domain,
/// task 148). OPEN DECISIONS #3: v1 is manual bank transfer - a batch is
/// created from the earning ledger, then an admin walks it through
/// Pending -&gt; Processing -&gt; Paid/Failed by hand; there is no gateway
/// integration.
/// </summary>
public interface IPartnerPayoutService
{
    /// <summary>Sums the partner's earning ledger over the period and creates a Pending payout for the net amount (must be positive).</summary>
    Task<Result<PartnerPayoutResponse>> CreateBatchAsync(Guid partnerId, CreatePartnerPayoutRequest request);

    Task<Result<PartnerPayoutResponse>> GetByIdAsync(Guid payoutId);

    Task<Result<PartnerPayoutSearchResponse>> SearchAsync(Guid? partnerId, PartnerPayoutStatus? status, int page, int pageSize);

    /// <summary>Advances a payout's status (Pending -&gt; Processing -&gt; Paid, or -&gt; Failed) - see <see cref="Domain.PartnerPayout"/>'s transition methods for the exact legal moves.</summary>
    Task<Result<PartnerPayoutResponse>> UpdateStatusAsync(Guid payoutId, UpdatePartnerPayoutStatusRequest request);
}
