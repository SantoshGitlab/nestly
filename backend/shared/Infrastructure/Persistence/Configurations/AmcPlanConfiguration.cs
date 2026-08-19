using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class AmcPlanConfiguration : IEntityTypeConfiguration<AmcPlan>
{
    public void Configure(EntityTypeBuilder<AmcPlan> builder)
    {
        builder.ToTable("amc_plan");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CategoryId).IsRequired();
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.HasIndex(x => x.Name).IsUnique();

        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Price).IsRequired().HasPrecision(12, 2);
        builder.Property(x => x.TermMonths).IsRequired();
        builder.Property(x => x.VisitsIncluded).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedByAdminUserId);
    }
}
