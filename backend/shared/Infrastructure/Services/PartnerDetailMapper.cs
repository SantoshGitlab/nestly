using Nestly.Application.PartnerManagement;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Builds a <see cref="PartnerDetailResponse"/> from a <see cref="Partner"/>
/// plus its KYC documents and background checks. Shared by
/// <see cref="PartnerManagementService"/> and <see cref="PartnerKycApprovalService"/>
/// so both services return the identical detail shape without duplicating
/// the mapping.
/// </summary>
internal static class PartnerDetailMapper
{
    public static PartnerDetailResponse ToDetailResponse(
        Partner partner,
        IReadOnlyList<PartnerKycDocument> documents,
        IReadOnlyList<PartnerBackgroundCheck> backgroundChecks) => new(
        partner.Id,
        partner.LegalName,
        partner.DisplayName,
        partner.PartnerType,
        partner.Phone,
        partner.Email,
        partner.Status,
        partner.OnboardingStatus,
        partner.CreatedAt,
        partner.UpdatedAt,
        documents.Select(d => new PartnerKycDocumentResponse(
            d.Id, d.DocType, d.DocNumber, d.FileRef, d.VerificationStatus, d.VerifiedBy, d.VerifiedAt, d.SubmittedAt)).ToList(),
        backgroundChecks.Select(c => new PartnerBackgroundCheckResponse(c.Id, c.Status, c.CheckedBy, c.CheckedAt, c.Notes)).ToList());
}
