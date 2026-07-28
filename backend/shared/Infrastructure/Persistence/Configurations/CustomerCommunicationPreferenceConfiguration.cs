using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CustomerCommunicationPreferenceConfiguration : IEntityTypeConfiguration<CustomerCommunicationPreference>
{
    public void Configure(EntityTypeBuilder<CustomerCommunicationPreference> builder)
    {
        builder.ToTable("customer_communication_preference");
        builder.HasKey(x => x.Id);

        // One row per customer: the unique index is what makes a concurrent
        // double-create fail loudly instead of silently leaving two
        // conflicting preference sets for the same person.
        builder.Property(x => x.CustomerId).IsRequired();
        builder.HasIndex(x => x.CustomerId).IsUnique();

        builder.Property(x => x.TransactionalSmsEnabled).IsRequired();
        builder.Property(x => x.TransactionalEmailEnabled).IsRequired();
        // Explicit names: the snake-case convention would otherwise split
        // "WhatsApp" into "whats_app".
        builder.Property(x => x.TransactionalWhatsAppEnabled)
            .HasColumnName("transactional_whatsapp_enabled").IsRequired();
        builder.Property(x => x.PromotionalSmsEnabled).IsRequired();
        builder.Property(x => x.PromotionalEmailEnabled).IsRequired();
        builder.Property(x => x.PromotionalWhatsAppEnabled)
            .HasColumnName("promotional_whatsapp_enabled").IsRequired();
        builder.Property(x => x.PushEnabled).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}
