using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class ExportJobConfiguration : IEntityTypeConfiguration<ExportJob>
{
    public void Configure(EntityTypeBuilder<ExportJob> builder)
    {
        builder.ToTable("export_job");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReportType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(x => x.FromUtc).IsRequired();
        builder.Property(x => x.ToUtc).IsRequired();
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.CategoryId);

        builder.Property(x => x.RequestedByAdminUserId).IsRequired();
        builder.HasIndex(x => x.RequestedByAdminUserId);

        builder.Property(x => x.RequestedAtUtc).IsRequired();
        builder.Property(x => x.CompletedAtUtc);

        builder.Property(x => x.ResultFileName).HasMaxLength(260);

        // No explicit size cap: a generated CSV can legitimately run larger
        // than any fixed varchar bound would allow; Postgres' bytea has no
        // practical size ceiling for the modest reports this queue
        // generates (SRS 12.18.2's "large" refers to elapsed *generation*
        // time, which is why this is asynchronous - not to unbounded row
        // counts).
        builder.Property(x => x.ResultContent);

        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
    }
}
