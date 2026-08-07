using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Configurations;

public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    /// <summary>
    /// The name is spelled out here and reused by the tests, so a rename shows
    /// up as a compile error rather than as a silently unasserted test.
    /// </summary>
    public const string ExactlyOneOwnerConstraintName = "ck_device_token_exactly_one_owner";

    /// <summary>
    /// <b>Task 277 - why two nullable FK columns and not owner_type/owner_id.</b>
    /// A polymorphic (owner_type, owner_id) pair cannot carry a foreign key to
    /// two different tables, so it would have traded a database-enforced
    /// invariant (this token belongs to a row that exists) for an
    /// application-enforced one, on the same table where we are simultaneously
    /// trying to *add* a database-enforced invariant. It would also have
    /// required rewriting every existing row (customer_id -> owner_id plus a
    /// literal owner_type) under a lock. The nullable-ProviderId shape keeps
    /// both FKs real, and existing customer rows migrate by doing nothing at
    /// all: customer_id keeps its value, provider_id defaults to NULL, and the
    /// CHECK below is already satisfied for every one of them. The only DDL
    /// touching the existing column is DROP NOT NULL, which rewrites no data.
    /// The cost is that "the owner" is two columns rather than one, which is
    /// why <see cref="DeviceTokenOwner"/> exists to make them behave as one.
    /// </summary>
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("device_token", t => t.HasCheckConstraint(
            ExactlyOneOwnerConstraintName,
            // Verbatim SQL: the snake-case naming convention rewrites property
            // names to columns, but not the inside of a constraint expression.
            // Written portably (IS NULL rather than a boolean XOR) because this
            // same predicate has to be legal on PostgreSQL at runtime and on
            // SQLite under the tests' EnsureCreated - see the note below.
            "(\"customer_id\" IS NOT NULL AND \"provider_id\" IS NULL) OR (\"customer_id\" IS NULL AND \"provider_id\" IS NOT NULL)"));

        builder.HasKey(x => x.Id);

        // Nullable since task 277: a device token is owned by a customer OR a
        // provider. Neither column is individually required; the CHECK above
        // requires exactly one, and DeviceTokenOwner enforces the same rule
        // before anything reaches the database.
        builder.Property(x => x.CustomerId);
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.ProviderId);
        builder.HasOne<Provider>()
            .WithMany()
            .HasForeignKey(x => x.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Computed from the two columns above; there is nothing to persist and
        // EF has no conversion for the struct.
        builder.Ignore(x => x.Owner);

        builder.Property(x => x.Platform).IsRequired().HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Token).IsRequired().HasMaxLength(500);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.RegisteredAtUtc).IsRequired();
        builder.Property(x => x.RevokedAtUtc);

        // A device/app-install is unique platform-wide (see DeviceToken's
        // doc comment on ReRegister) - not scoped per owner.
        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => new { x.CustomerId, x.IsActive });
        builder.HasIndex(x => new { x.ProviderId, x.IsActive });
    }
}
