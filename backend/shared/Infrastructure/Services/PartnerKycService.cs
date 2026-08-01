using Nestly.Application;
using Nestly.Application.PartnerIdentity;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// KYC document submission and status lookup (task 146c). Only the
/// submission side - approval/rejection is task 150b's admin workflow,
/// which will call <see cref="PartnerKycDocument.Approve"/>/
/// <see cref="PartnerKycDocument.Reject"/> directly.
/// </summary>
public class PartnerKycService : IPartnerKycService
{
    private readonly IPartnerRepository _partnerRepository;
    private readonly IPartnerKycDocumentRepository _kycDocumentRepository;

    public PartnerKycService(IPartnerRepository partnerRepository, IPartnerKycDocumentRepository kycDocumentRepository)
    {
        _partnerRepository = partnerRepository;
        _kycDocumentRepository = kycDocumentRepository;
    }

    public async Task<Result<PartnerKycDocumentResponse>> SubmitDocumentAsync(SubmitPartnerKycDocumentRequest request)
    {
        var partner = await _partnerRepository.GetByIdAsync(request.PartnerId);
        if (partner is null)
        {
            return Result.Failure<PartnerKycDocumentResponse>(
                Error.NotFound("PartnerKyc.PartnerNotFound", "No partner found for this id."));
        }

        var document = new PartnerKycDocument(
            Guid.NewGuid(), request.PartnerId, request.DocType, request.FileRef, request.DocNumber);
        await _kycDocumentRepository.AddAsync(document);

        // Advances onboarding to KycSubmitted the first time a document is
        // submitted (idempotent - see Partner.MarkKycSubmitted).
        partner.MarkKycSubmitted();
        await _partnerRepository.UpdateAsync(partner);

        return Result.Success(ToResponse(document));
    }

    public async Task<Result<PartnerKycStatusResponse>> GetStatusAsync(Guid partnerId)
    {
        var partner = await _partnerRepository.GetByIdAsync(partnerId);
        if (partner is null)
        {
            return Result.Failure<PartnerKycStatusResponse>(
                Error.NotFound("PartnerKyc.PartnerNotFound", "No partner found for this id."));
        }

        var documents = await _kycDocumentRepository.GetByPartnerAsync(partnerId);

        return Result.Success(new PartnerKycStatusResponse(
            partnerId,
            partner.OnboardingStatus.ToString(),
            documents.Select(ToResponse).ToList()));
    }

    private static PartnerKycDocumentResponse ToResponse(PartnerKycDocument document) => new(
        document.Id,
        document.PartnerId,
        document.DocType.ToString(),
        document.DocNumber,
        document.FileRef,
        document.VerificationStatus.ToString(),
        document.SubmittedAt,
        document.VerifiedAt);
}
