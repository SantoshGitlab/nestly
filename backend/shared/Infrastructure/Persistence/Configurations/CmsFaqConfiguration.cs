using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CmsFaqConfiguration : IEntityTypeConfiguration<CmsFaq>
{
    public void Configure(EntityTypeBuilder<CmsFaq> builder)
    {
        builder.ToTable("cms_faq");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Question).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Answer).IsRequired();
        builder.Property(x => x.Placement).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.PublishStartUtc);
        builder.Property(x => x.PublishEndUtc);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.Placement, x.Status, x.SortOrder });
    }
}
