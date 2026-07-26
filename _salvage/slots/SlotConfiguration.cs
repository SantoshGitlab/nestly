namespace backend.shared.Infrastructure.Persistence.Configurations
{
    public class SlotConfiguration : IEntityTypeConfiguration<Slot>
    {
        public void Configure(EntityTypeBuilder<Slot> builder)
        {
            builder.Property(b => b.StartTime).IsRequired();
            builder.Property(b => b.EndTime).IsRequired();
            builder.Property(b => b.Capacity).IsRequired().HasDefaultValue(1);
            builder.Property(b => b.IsBlackout).IsRequired().HasDefaultValue(false);
        }
    }
}
