using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Persistence for <see cref="PartnerPayout"/> (task 148).</summary>
public interface IPartnerPayoutRepository
{
    Task AddAsync(PartnerPayout entity);
    Task UpdateAsync(PartnerPayout entity);
    Task<PartnerPayout?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<PartnerPayout>> ListByPartnerAsync(Guid partnerId);

    /// <summary>Admin-facing, paginated payout list (task 150c "run payout batch, list payouts").</summary>
    Task<(IReadOnlyList<PartnerPayout> Rows, int TotalCount)> SearchAsync(Guid? partnerId, PartnerPayoutStatus? status, int page, int pageSize);
}
