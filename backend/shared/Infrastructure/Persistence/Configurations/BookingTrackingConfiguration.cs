using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class BookingTrackingConfiguration : IEntityTypeConfiguration<BookingTracking>
{
    public void Configure(EntityTypeBuilder<BookingTracking> builder)
    {
        builder.ToTable("booking_tracking");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BookingId).IsRequired();
        builder.Property(x => x.ProviderId);

        builder.Property(x => x.EtaSeconds);
        builder.Property(x => x.EtaDistanceMetres);
        builder.Property(x => x.EtaComputedAtUtc);
        builder.Property(x => x.EtaSource).HasConversion<string>().HasMaxLength(20);

        // Same decimal(9,6) shape as ProviderConfiguration's and
        // ProviderLocationPingConfiguration's coordinates: this pair is copied
        // straight off a ping, and a column that stored it at a different
        // resolution would make the movement throttle measure a drift the
        // provider never made.
        builder.Property(x => x.EtaOriginLatitude).HasPrecision(9, 6);
        builder.Property(x => x.EtaOriginLongitude).HasPrecision(9, 6);

        // One tracking row per booking, enforced by the database rather than
        // by the service remembering to check: two rows would give the
        // customer read model and the admin live-ops list two different ETAs
        // for the same job depending on which one they happened to read.
        builder.HasIndex(x => x.BookingId).IsUnique();
    }
}
