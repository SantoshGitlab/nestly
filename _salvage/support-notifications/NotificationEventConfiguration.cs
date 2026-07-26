using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using backend.shared.Application.Domain;

namespace backend.shared.Infrastructure.Persistence.Configurations
{
    public class NotificationEventConfiguration : IEntityTypeConfiguration<NotificationEvent>
    {
        public void Configure(EntityTypeBuilder<NotificationEvent> builder)
        {
            builder.ToTable("notification_events");

            builder.HasKey(ne => ne.Id);

            builder.Property(ne => ne.Id).UseIdentityColumn();

            builder.Property(ne => ne.Title)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(ne => ne.Content)
                .IsRequired()
                .HasColumnType("text");

            builder.Property(ne => ne.CreatedAt)
                .IsRequired();

            builder.Property(ne => ne.Type)
                .IsRequired();

            builder.HasOne(ne => ne.Customer)
                .WithMany(c => c.NotificationEvents)
                .HasForeignKey(ne => ne.CustomerId);
        }
    }
}
