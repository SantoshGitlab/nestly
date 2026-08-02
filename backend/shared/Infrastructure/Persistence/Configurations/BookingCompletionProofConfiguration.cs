using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class BookingCompletionProofConfiguration : IEntityTypeConfiguration<BookingCompletionProof>
{
    public void Configure(EntityTypeBuilder<BookingCompletionProof> builder)
    {
        builder.ToTable("booking_completion_proof");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BookingId).IsRequired();
        builder.HasOne<Booking>()
            .WithOne()
            .HasForeignKey<BookingCompletionProof>(x => x.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        // One proof per booking (task 195/196 - the guard only ever needs
        // "does one exist", and a resubmission updates this row rather than
        // adding another - see BookingCompletionProof.Update).
        builder.HasIndex(x => x.BookingId).IsUnique();

        builder.Property(x => x.SubmittedByPartnerId).IsRequired();
        builder.Property(x => x.SubmittedAtUtc).IsRequired();

        builder.Property(x => x.PhotoRefsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ChecklistAnswersJson).HasColumnType("jsonb").IsRequired();
    }
}
