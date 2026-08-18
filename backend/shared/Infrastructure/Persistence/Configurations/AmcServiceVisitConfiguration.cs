using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class AmcServiceVisitConfiguration : IEntityTypeConfiguration<AmcServiceVisit>
{
    public void Configure(EntityTypeBuilder<AmcServiceVisit> builder)
    {
        builder.ToTable("amc_service_visit");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContractId).IsRequired();
        builder.HasOne<CustomerAmcContract>()
            .WithMany()
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.BookingId).IsRequired();
        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.ConsumedAtUtc).IsRequired();

        builder.HasIndex(x => x.ContractId);
        // One visit per booking - the same booking can never be redeemed against twice.
        builder.HasIndex(x => x.BookingId).IsUnique();
    }
}
