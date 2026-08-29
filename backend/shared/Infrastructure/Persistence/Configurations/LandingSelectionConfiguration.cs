using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class LandingSelectionConfiguration : IEntityTypeConfiguration<LandingSelection>
{
    public void Configure(EntityTypeBuilder<LandingSelection> builder)
    {
        builder.ToTable("landing_selection");
        builder.HasKey(x => x.Id);

        // Stored as the enum's int, matching how every other enum in this
        // schema is persisted (no JsonStringEnumConverter is registered).
        builder.Property(x => x.SectionType).IsRequired().HasConversion<int>();

        // Both are nullable at the schema level because which one applies
        // depends on SectionType; the domain's factory methods are what
        // guarantee a row is never written with the wrong combination.
        builder.Property(x => x.CategoryId);
        builder.Property(x => x.ServiceId);
        builder.Property(x => x.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // The read path always filters by section first, then orders - and
        // the category strip additionally groups by its heading category.
        builder.HasIndex(x => new { x.SectionType, x.SortOrder });
        builder.HasIndex(x => new { x.SectionType, x.CategoryId, x.SortOrder });
    }
}
