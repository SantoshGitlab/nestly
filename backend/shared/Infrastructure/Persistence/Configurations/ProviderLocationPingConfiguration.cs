using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ProviderLocationPingConfiguration : IEntityTypeConfiguration<ProviderLocationPing>
{
    public void Configure(EntityTypeBuilder<ProviderLocationPing> builder)
    {
        builder.ToTable("provider_location_ping");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderId).IsRequired();
        builder.Property(x => x.BookingId);

        // Task 268. Same precision as ProviderConfiguration's
        // Latitude/Longitude - a fix that round-trips at a different
        // resolution to the last-known pair it feeds would make the two
        // disagree about the same point.
        builder.Property(x => x.Latitude).HasPrecision(9, 6).IsRequired();
        builder.Property(x => x.Longitude).HasPrecision(9, 6).IsRequired();

        // Metres, one decimal place: devices report accuracy as a radius in
        // metres and nothing reads it more finely than that.
        builder.Property(x => x.AccuracyMetres).HasPrecision(8, 1);

        builder.Property(x => x.RecordedAtUtc).IsRequired();
        builder.Property(x => x.ReceivedAtUtc).IsRequired();
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(20).IsRequired();

        // The trail read for one booking, oldest fix first.
        builder.HasIndex(x => new { x.BookingId, x.RecordedAtUtc });

        // The latest-fix read for one provider, which is not the same access
        // path: idle pings carry no BookingId, so the index above cannot serve
        // "where is this provider now".
        builder.HasIndex(x => new { x.ProviderId, x.RecordedAtUtc });
    }
}
