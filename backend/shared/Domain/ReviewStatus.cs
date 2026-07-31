namespace Nestly.Domain;

/// <summary>Review visibility, moderated by admin (SRS 11.16.3, 17.2).</summary>
public enum ReviewStatus
{
    Visible,
    Hidden,
    Flagged
}
