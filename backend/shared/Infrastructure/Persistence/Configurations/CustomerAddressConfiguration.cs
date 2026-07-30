using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("customer_address");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CustomerId).IsRequired();
        builder.Property(x => x.Label).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Line1).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Line2).HasMaxLength(300);
        builder.Property(x => x.Landmark).HasMaxLength(200);
        builder.Property(x => x.Pincode).IsRequired().HasMaxLength(12);
        builder.Property(x => x.City).IsRequired().HasMaxLength(100);
        builder.Property(x => x.State).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Latitude).HasColumnType("decimal(9,6)").IsRequired();
        builder.Property(x => x.Longitude).HasColumnType("decimal(9,6)").IsRequired();
        builder.Property(x => x.ContactName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ContactMobile).IsRequired().HasMaxLength(20);
        builder.Property(x => x.IsDefault).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        // Best-effort links into the geography master (see CustomerAddress's
        // doc comments). SetNull rather than Restrict/Cascade: if a pincode
        // or locality is later deleted, the address itself - and its raw
        // Pincode/City/State text - must remain valid; only the link breaks.
        builder.HasOne<Pincode>()
            .WithMany()
            .HasForeignKey(x => x.PincodeId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Locality>()
            .WithMany()
            .HasForeignKey(x => x.LocalityId)
            .OnDelete(DeleteBehavior.SetNull);

        // At most one default address per customer — enforced at the
        // database level (partial unique index), not only in the service.
        // A second plain index also exists for general "all addresses for
        // customer" lookups. Both must have distinct explicit names or EF
        // Core merges them into a single index; UseSnakeCaseNamingConvention
        // additionally overrides one of the two names on generation, so the
        // actual column in Postgres ends up named ..._customer_id1 rather
        // than the name given here — functionally still a separate index.
        builder.HasIndex(x => x.CustomerId, "ix_customer_address_one_default_per_customer")
            .IsUnique()
            .HasFilter("is_default = true");

        builder.HasIndex(x => x.CustomerId, "ix_customer_address_all");
    }
}
