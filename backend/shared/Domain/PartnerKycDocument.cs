using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>Category of KYC document a partner can submit (PARTNER.md "partner_kyc_document").</summary>
public enum PartnerKycDocumentType
{
    IdentityProof,
    AddressProof,
    BankAccountProof,
    ProfessionalCertificate,
    Other
}

/// <summary>
/// Review outcome of one submitted document. Approval/rejection is task
/// 150b (admin-side, not built here) - this module only builds the
/// submission side (task 146c) and exposes the transition methods for that
/// future admin workflow to call.
/// </summary>
public enum PartnerKycVerificationStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// One KYC document submitted by a partner (PARTNER.md "partner_kyc_document:
/// doc_type, doc_number, file_ref, verification_status, verified_by,
/// verified_at"). <see cref="FileRef"/> is a reference (storage key/URL) to
/// the uploaded file, not the file content itself - matching how the rest of
/// this codebase handles media (see <c>CmsMedia</c>).
/// </summary>
public class PartnerKycDocument : Entity<Guid>
{
    public Guid PartnerId { get; private set; }
    public PartnerKycDocumentType DocType { get; private set; }
    public string? DocNumber { get; private set; }
    public string FileRef { get; private set; } = string.Empty;
    public PartnerKycVerificationStatus VerificationStatus { get; private set; }
    public Guid? VerifiedBy { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public DateTime SubmittedAt { get; private set; }

    protected PartnerKycDocument() { }

    public PartnerKycDocument(Guid id, Guid partnerId, PartnerKycDocumentType docType, string fileRef, string? docNumber = null)
        : base(id)
    {
        PartnerId = partnerId;
        DocType = docType;
        FileRef = string.IsNullOrWhiteSpace(fileRef)
            ? throw new ArgumentException("A file reference is required.", nameof(fileRef))
            : fileRef;
        DocNumber = docNumber;
        VerificationStatus = PartnerKycVerificationStatus.Pending;
        SubmittedAt = DateTime.UtcNow;
    }

    /// <summary>Admin approval (task 150b). Not called by anything in this pass - built for that future workflow.</summary>
    public void Approve(Guid verifiedByAdminUserId)
    {
        VerificationStatus = PartnerKycVerificationStatus.Approved;
        VerifiedBy = verifiedByAdminUserId;
        VerifiedAt = DateTime.UtcNow;
    }

    /// <summary>Admin rejection (task 150b). Not called by anything in this pass - built for that future workflow.</summary>
    public void Reject(Guid verifiedByAdminUserId)
    {
        VerificationStatus = PartnerKycVerificationStatus.Rejected;
        VerifiedBy = verifiedByAdminUserId;
        VerifiedAt = DateTime.UtcNow;
    }
}
