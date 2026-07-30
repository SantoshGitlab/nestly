using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class SlotWindowRuleConfiguration : IEntityTypeConfiguration<SlotWindowRule>
{
    public void Configure(EntityTypeBuilder<SlotWindowRule> builder)
    {
        builder.ToTable("slot_window_rule");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DayOfWeek).IsRequired();

        builder.Property(x => x.SlotWindowId).IsRequired();
        builder.HasOne(x => x.SlotWindow)
            .WithMany()
            .HasForeignKey(x => x.SlotWindowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SlotWindowId, x.DayOfWeek }).IsUnique();
    }
}
