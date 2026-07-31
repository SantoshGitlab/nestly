using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class PartnerKycDocumentConfiguration : IEntityTypeConfiguration<PartnerKycDocument>
{
    public void Configure(EntityTypeBuilder<PartnerKycDocument> builder)
    {
        builder.ToTable("partner_kyc_document");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PartnerId).IsRequired();
        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PartnerId);

        builder.Property(x => x.DocType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.DocNumber).HasMaxLength(100);
        builder.Property(x => x.FileRef).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.VerifiedBy);
        builder.Property(x => x.VerifiedAt);
        builder.Property(x => x.SubmittedAt).IsRequired();
    }
}
