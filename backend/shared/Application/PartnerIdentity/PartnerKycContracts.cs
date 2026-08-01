using Nestly.Domain;

namespace Nestly.Application.PartnerIdentity;

/// <summary>
/// KYC document submission (task 146c, PARTNER.md API surface "upload KYC
/// documents"). <see cref="FileRef"/> is a reference to an already-uploaded
/// file (storage key/URL) - this workflow does not itself handle the binary
/// upload/storage concern, matching how <c>PartnerKycDocument.FileRef</c> is
/// modeled.
/// </summary>
public record SubmitPartnerKycDocumentRequest(
    Guid PartnerId,
    PartnerKycDocumentType DocType,
    string FileRef,
    string? DocNumber);

public record PartnerKycDocumentResponse(
    Guid Id,
    Guid PartnerId,
    string DocType,
    string? DocNumber,
    string FileRef,
    string VerificationStatus,
    DateTime SubmittedAt,
    DateTime? VerifiedAt);

/// <summary>Overall KYC picture for a partner (PARTNER.md API surface "get KYC status").</summary>
public record PartnerKycStatusResponse(
    Guid PartnerId,
    string OnboardingStatus,
    IReadOnlyList<PartnerKycDocumentResponse> Documents);
