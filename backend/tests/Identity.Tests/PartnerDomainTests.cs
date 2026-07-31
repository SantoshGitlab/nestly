using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Identity.Tests;

/// <summary>
/// Pure domain rules on <see cref="Partner"/> and <see cref="PartnerKycDocument"/>
/// (task 145a/145b, PARTNER.md OPEN DECISIONS). No database involved - these
/// are invariants the entity itself enforces regardless of persistence.
/// </summary>
public class PartnerDomainTests
{
    [Fact]
    public void A_company_partner_cannot_be_created_in_this_release()
    {
        // OPEN DECISIONS #2: individuals only for v1.
        Action act = () => new Partner(
            Guid.NewGuid(), "Acme Services Pvt Ltd", "Acme Services", PartnerType.Company, "+919876543210");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void An_individual_partner_starts_pending_verification_with_registered_onboarding()
    {
        var partner = new Partner(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", PartnerType.Individual, "+919876543210");

        partner.Status.Should().Be(PartnerStatus.PendingVerification);
        partner.OnboardingStatus.Should().Be(PartnerOnboardingStatus.Registered);
    }

    [Fact]
    public void A_blank_legal_name_is_rejected()
    {
        Action act = () => new Partner(Guid.NewGuid(), "  ", "Ravi's Repairs", PartnerType.Individual, "+919876543210");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateProfile_advances_onboarding_from_registered_to_profile_completed()
    {
        var partner = new Partner(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", PartnerType.Individual, "+919876543210");

        partner.UpdateProfile("Ravi Kumar", "Ravi's Home Repairs", "ravi@example.com");

        partner.OnboardingStatus.Should().Be(PartnerOnboardingStatus.ProfileCompleted);
    }

    [Fact]
    public void MarkKycSubmitted_is_idempotent_and_does_not_regress_a_later_onboarding_state()
    {
        var partner = new Partner(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", PartnerType.Individual, "+919876543210");
        partner.MarkKycSubmitted();
        partner.OnboardingStatus.Should().Be(PartnerOnboardingStatus.KycSubmitted);

        // A second submission (e.g. a re-upload) must not move the funnel backwards.
        partner.MarkKycSubmitted();
        partner.OnboardingStatus.Should().Be(PartnerOnboardingStatus.KycSubmitted);
    }

    [Fact]
    public void A_kyc_document_starts_pending_and_records_the_admin_who_approves_it()
    {
        var adminUserId = Guid.NewGuid();
        var document = new PartnerKycDocument(
            Guid.NewGuid(), Guid.NewGuid(), PartnerKycDocumentType.IdentityProof, "s3://kyc/doc.pdf");

        document.VerificationStatus.Should().Be(PartnerKycVerificationStatus.Pending);

        document.Approve(adminUserId);

        document.VerificationStatus.Should().Be(PartnerKycVerificationStatus.Approved);
        document.VerifiedBy.Should().Be(adminUserId);
        document.VerifiedAt.Should().NotBeNull();
    }
}
