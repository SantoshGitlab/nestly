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
    public class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
    {
        public void Configure(EntityTypeBuilder<SupportTicket> builder)
        {
            builder.ToTable("support_tickets");

            builder.HasKey(st => st.Id);

            builder.Property(st => st.Id).UseIdentityColumn();

            builder.Property(st => st.Title)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(st => st.Description)
                .IsRequired()
                .HasColumnType("text");

            builder.Property(st => st.CreatedAt)
                .IsRequired();

            builder.Property(st => st.UpdatedAt)
                .IsRequired();

            builder.HasOne(st => st.Customer)
                .WithMany(c => c.SupportTickets)
                .HasForeignKey(st => st.CustomerId);
        }
    }
}
