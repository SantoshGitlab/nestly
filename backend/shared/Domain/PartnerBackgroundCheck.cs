using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>Outcome of a background/reference check (task 160).</summary>
public enum PartnerBackgroundCheckStatus
{
    Pending,
    Passed,
    Failed
}

/// <summary>
/// A distinct post-KYC background/reference check step (task 160,
/// PARTNER.md) - police verification, prior work history, etc. Kept as its
/// own small entity rather than a field on <see cref="Partner"/> because it
/// is a policy-configurable, admin-recorded event with its own
/// who/when/why (<see cref="CheckedBy"/>/<see cref="CheckedAt"/>/<see cref="Notes"/>),
/// separate from KYC document validation (task 150b). Append-only, like
/// <see cref="PartnerKycDocument"/> - a re-check creates a new row rather
/// than overwriting the previous outcome, so <see cref="Partner"/>'s
/// activation gate always reads the most recent one
/// (<c>IPartnerBackgroundCheckRepository.GetLatestByPartnerAsync</c>).
/// </summary>
public class PartnerBackgroundCheck : Entity<Guid>
{
    public Guid PartnerId { get; private set; }
    public PartnerBackgroundCheckStatus Status { get; private set; }
    public Guid CheckedBy { get; private set; }
    public DateTime CheckedAt { get; private set; }
    public string? Notes { get; private set; }

    protected PartnerBackgroundCheck() { }

    public PartnerBackgroundCheck(Guid id, Guid partnerId, PartnerBackgroundCheckStatus status, Guid checkedBy, string? notes)
        : base(id)
    {
        if (status == PartnerBackgroundCheckStatus.Pending)
        {
            throw new ArgumentException("A background check record must be created with a final Passed/Failed outcome.", nameof(status));
        }

        PartnerId = partnerId;
        Status = status;
        CheckedBy = checkedBy;
        CheckedAt = DateTime.UtcNow;
        Notes = notes;
    }
}
