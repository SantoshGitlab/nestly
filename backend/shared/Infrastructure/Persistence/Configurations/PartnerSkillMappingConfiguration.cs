using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class PartnerSkillMappingConfiguration : IEntityTypeConfiguration<PartnerSkillMapping>
{
    public void Configure(EntityTypeBuilder<PartnerSkillMapping> builder)
    {
        builder.ToTable("partner_skill_mapping");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.PartnerId).IsRequired();
        builder.HasOne<Partner>()
            .WithMany()
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CategoryId).IsRequired();
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.ServiceId);
        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PartnerId, x.CategoryId, x.ServiceId }).IsUnique();
        builder.HasIndex(x => x.CategoryId);
    }
}
