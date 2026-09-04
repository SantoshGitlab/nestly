using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CmsMediaConfiguration : IEntityTypeConfiguration<CmsMedia>
{
    public void Configure(EntityTypeBuilder<CmsMedia> builder)
    {
        builder.ToTable("cms_media");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Url).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.AltText).HasMaxLength(300);
        builder.Property(x => x.MediaType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
    }
}
