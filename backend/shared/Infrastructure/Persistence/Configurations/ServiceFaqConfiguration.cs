using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ServiceFaqConfiguration : IEntityTypeConfiguration<ServiceFaq>
{
    public void Configure(EntityTypeBuilder<ServiceFaq> builder)
    {
        builder.ToTable("service_faq");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceId).IsRequired();
        builder.HasIndex(x => x.ServiceId);
        builder.Property(x => x.Question).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Answer).IsRequired().HasMaxLength(2000);
    }
}
