using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ServiceMediaConfiguration : IEntityTypeConfiguration<ServiceMedia>
{
    public void Configure(EntityTypeBuilder<ServiceMedia> builder)
    {
        builder.ToTable("service_media");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceId).IsRequired();
        builder.HasIndex(x => x.ServiceId);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(1000);
    }
}
