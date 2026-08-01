using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nestly.Application.PartnerIdentity;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Identity.Tests;

/// <summary>
/// KYC document submission and status lookup (task 146c, submission side
/// only - approval/rejection is task 150b, not exercised here).
/// </summary>
public class PartnerKycServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private Guid _partnerId;

    public PartnerKycServiceTests()
    {
        using var context = _database.CreateContext();
        var partner = new Partner(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", PartnerType.Individual, "+919876543210");
        _partnerId = partner.Id;
        context.Add(partner);
        context.SaveChanges();
    }

    private PartnerKycService CreateService(NestlyDbContext context) =>
        new(new PartnerRepository(context), new PartnerKycDocumentRepository(context));

    [Fact]
    public async Task SubmitDocumentAsync_stores_the_document_as_pending()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).SubmitDocumentAsync(
            new SubmitPartnerKycDocumentRequest(_partnerId, PartnerKycDocumentType.IdentityProof, "s3://kyc/doc1.pdf", "AB1234567"));

        result.IsSuccess.Should().BeTrue();
        result.Value.VerificationStatus.Should().Be(nameof(PartnerKycVerificationStatus.Pending));

        var stored = await context.Set<PartnerKycDocument>().SingleAsync();
        stored.PartnerId.Should().Be(_partnerId);
        stored.DocType.Should().Be(PartnerKycDocumentType.IdentityProof);
        stored.VerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task SubmitDocumentAsync_advances_onboarding_status_to_kyc_submitted()
    {
        await using var context = _database.CreateContext();
        await CreateService(context).SubmitDocumentAsync(
            new SubmitPartnerKycDocumentRequest(_partnerId, PartnerKycDocumentType.AddressProof, "s3://kyc/doc2.pdf", null));

        var partner = await context.Set<Partner>().SingleAsync(p => p.Id == _partnerId);
        partner.OnboardingStatus.Should().Be(PartnerOnboardingStatus.KycSubmitted);
    }

    [Fact]
    public async Task SubmitDocumentAsync_rejects_an_unknown_partner()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).SubmitDocumentAsync(
            new SubmitPartnerKycDocumentRequest(Guid.NewGuid(), PartnerKycDocumentType.IdentityProof, "s3://kyc/doc.pdf", null));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PartnerKyc.PartnerNotFound");
    }

    [Fact]
    public async Task GetStatusAsync_returns_every_submitted_document_with_the_onboarding_status()
    {
        await using var context = _database.CreateContext();
        var service = CreateService(context);
        await service.SubmitDocumentAsync(
            new SubmitPartnerKycDocumentRequest(_partnerId, PartnerKycDocumentType.IdentityProof, "s3://kyc/id.pdf", null));
        await service.SubmitDocumentAsync(
            new SubmitPartnerKycDocumentRequest(_partnerId, PartnerKycDocumentType.AddressProof, "s3://kyc/addr.pdf", null));

        var status = await service.GetStatusAsync(_partnerId);

        status.IsSuccess.Should().BeTrue();
        status.Value.Documents.Should().HaveCount(2);
        status.Value.OnboardingStatus.Should().Be(nameof(PartnerOnboardingStatus.KycSubmitted));
    }

    [Fact]
    public async Task GetStatusAsync_rejects_an_unknown_partner()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).GetStatusAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PartnerKyc.PartnerNotFound");
    }

    public void Dispose() => _database.Dispose();
}
