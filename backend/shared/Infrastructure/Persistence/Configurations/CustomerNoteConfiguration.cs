using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CustomerNoteConfiguration : IEntityTypeConfiguration<CustomerNote>
{
    public void Configure(EntityTypeBuilder<CustomerNote> builder)
    {
        builder.ToTable("customer_note");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CustomerId).IsRequired();
        builder.Property(x => x.AuthorAdminUserId).IsRequired();
        builder.Property(x => x.Note).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // Every read of this table is "notes for customer X, newest first" -
        // see ICustomerNoteRepository.ListByCustomerAsync.
        builder.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc });
    }
}
