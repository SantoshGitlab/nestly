using Nestly.Application;
using Nestly.Application.PartnerManagement;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="IPartnerKycApprovalService"/>
public class PartnerKycApprovalService : IPartnerKycApprovalService
{
    private readonly IPartnerRepository _partnerRepository;
    private readonly IPartnerKycDocumentRepository _kycDocumentRepository;
    private readonly IPartnerBackgroundCheckRepository _backgroundCheckRepository;

    public PartnerKycApprovalService(
        IPartnerRepository partnerRepository,
        IPartnerKycDocumentRepository kycDocumentRepository,
        IPartnerBackgroundCheckRepository backgroundCheckRepository)
    {
        _partnerRepository = partnerRepository;
        _kycDocumentRepository = kycDocumentRepository;
        _backgroundCheckRepository = backgroundCheckRepository;
    }

    public async Task<Result<PartnerKycDocumentResponse>> ApproveDocumentAsync(Guid documentId, Guid adminUserId)
    {
        var document = await _kycDocumentRepository.GetByIdAsync(documentId);
        if (document is null)
        {
            return Error.NotFound("PartnerKycApproval.DocumentNotFound", "KYC document was not found.");
        }

        if (document.VerificationStatus != PartnerKycVerificationStatus.Pending)
        {
            return Error.Business("PartnerKycApproval.AlreadyReviewed", $"This document was already {document.VerificationStatus}.");
        }

        document.Approve(adminUserId);
        await _kycDocumentRepository.UpdateAsync(document);

        // Advances onboarding to KycVerified the first time a document is
        // approved (idempotent - see Partner.MarkKycVerified).
        var partner = await _partnerRepository.GetByIdAsync(document.PartnerId);
        if (partner is not null)
        {
            partner.MarkKycVerified();
            await _partnerRepository.UpdateAsync(partner);
        }

        return ToResponse(document);
    }

    public async Task<Result<PartnerKycDocumentResponse>> RejectDocumentAsync(Guid documentId, Guid adminUserId, RejectPartnerKycDocumentRequest request)
    {
        var document = await _kycDocumentRepository.GetByIdAsync(documentId);
        if (document is null)
        {
            return Error.NotFound("PartnerKycApproval.DocumentNotFound", "KYC document was not found.");
        }

        if (document.VerificationStatus != PartnerKycVerificationStatus.Pending)
        {
            return Error.Business("PartnerKycApproval.AlreadyReviewed", $"This document was already {document.VerificationStatus}.");
        }

        document.Reject(adminUserId);
        await _kycDocumentRepository.UpdateAsync(document);

        return ToResponse(document);
    }

    public async Task<Result<PartnerBackgroundCheckResponse>> RecordBackgroundCheckAsync(Guid partnerId, Guid adminUserId, RecordBackgroundCheckRequest request)
    {
        if (!await _partnerRepository.ExistsAsync(partnerId))
        {
            return Error.NotFound("PartnerKycApproval.PartnerNotFound", "Partner was not found.");
        }

        if (request.Status == PartnerBackgroundCheckStatus.Pending)
        {
            return Error.Validation("PartnerKycApproval.InvalidStatus", "A background check must be recorded with a final Passed/Failed outcome.");
        }

        var check = new PartnerBackgroundCheck(Guid.NewGuid(), partnerId, request.Status, adminUserId, request.Notes);
        await _backgroundCheckRepository.AddAsync(check);

        return new PartnerBackgroundCheckResponse(check.Id, check.Status, check.CheckedBy, check.CheckedAt, check.Notes);
    }

    public async Task<Result<PartnerDetailResponse>> ActivateAsync(Guid partnerId)
    {
        var partner = await _partnerRepository.GetByIdAsync(partnerId);
        if (partner is null)
        {
            return Error.NotFound("PartnerKycApproval.PartnerNotFound", "Partner was not found.");
        }

        if (partner.Status == PartnerStatus.Active)
        {
            return Error.Business("PartnerKycApproval.AlreadyActive", "This partner is already active.");
        }

        if (partner.Status != PartnerStatus.PendingVerification)
        {
            return Error.Business("PartnerKycApproval.InvalidStatus", $"A partner can only be activated from PendingVerification (current status: {partner.Status}).");
        }

        // Task 160's gate: both KYC approval and a passed background check
        // are required before a partner can go fully Active.
        if (partner.OnboardingStatus != PartnerOnboardingStatus.KycVerified && partner.OnboardingStatus != PartnerOnboardingStatus.Completed)
        {
            return Error.Business("PartnerKycApproval.KycNotVerified", "At least one KYC document must be approved before this partner can be activated.");
        }

        var latestCheck = await _backgroundCheckRepository.GetLatestByPartnerAsync(partnerId);
        if (latestCheck is null || latestCheck.Status != PartnerBackgroundCheckStatus.Passed)
        {
            return Error.Business("PartnerKycApproval.BackgroundCheckRequired", "A passed background check is required before this partner can be activated.");
        }

        partner.ChangeStatus(PartnerStatus.Active);
        partner.MarkOnboardingCompleted();
        await _partnerRepository.UpdateAsync(partner);

        var documents = await _kycDocumentRepository.GetByPartnerAsync(partnerId);
        var backgroundChecks = await _backgroundCheckRepository.ListByPartnerAsync(partnerId);

        return PartnerDetailMapper.ToDetailResponse(partner, documents, backgroundChecks);
    }

    private static PartnerKycDocumentResponse ToResponse(PartnerKycDocument document) => new(
        document.Id, document.DocType, document.DocNumber, document.FileRef,
        document.VerificationStatus, document.VerifiedBy, document.VerifiedAt, document.SubmittedAt);
}
