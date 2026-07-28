using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Identity.Tests;

/// <summary>
/// The uniqueness rules that must hold at the database level, not only in
/// service code (SRS 11.2.1 unique mobile/email, 11.3.3 one default address).
///
/// These run against a real relational engine on purpose: a service-layer
/// "does it already exist?" check is racy, and only the index makes the rule
/// actually unbreakable. The EF in-memory provider would report a false pass
/// here because it does not implement indexes at all.
/// </summary>
public class UniquenessConstraintTests : IDisposable
{
    private readonly TestDatabase _database = new();

    private static Customer NewCustomer(string mobile, string? email = null) =>
        new(Guid.NewGuid(), mobile, "Test Customer", CustomerStatus.Active, email);

    [Fact]
    public async Task Two_customers_cannot_share_a_mobile_number()
    {
        await using (var context = _database.CreateContext())
        {
            context.Add(NewCustomer("+919876543210"));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            context.Add(NewCustomer("+919876543210"));

            var save = async () => await context.SaveChangesAsync();
            await save.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task Two_customers_cannot_share_an_email_address()
    {
        await using (var context = _database.CreateContext())
        {
            context.Add(NewCustomer("+919876543210", "shared@example.com"));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            context.Add(NewCustomer("+919000000001", "shared@example.com"));

            var save = async () => await context.SaveChangesAsync();
            await save.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task Many_customers_may_have_no_email_at_all()
    {
        await using var context = _database.CreateContext();

        // The email index is filtered on "email IS NOT NULL" precisely so
        // that mobile-only registration (SRS 11.2.1) is not limited to one
        // customer. Without the filter this is the case that would break.
        context.Add(NewCustomer("+919876543210"));
        context.Add(NewCustomer("+919000000001"));
        context.Add(NewCustomer("+919000000002"));

        await context.SaveChangesAsync();

        (await context.Set<Customer>().CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task One_identifier_cannot_be_registered_twice_for_the_same_provider()
    {
        var customerId = Guid.NewGuid();

        await using (var context = _database.CreateContext())
        {
            context.Add(new CustomerAuthIdentity(
                Guid.NewGuid(), customerId, AuthProviderType.EmailPassword, "user@example.com", isPrimary: false));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            // Two accounts answering to one email+password login would make
            // authentication ambiguous.
            context.Add(new CustomerAuthIdentity(
                Guid.NewGuid(), Guid.NewGuid(), AuthProviderType.EmailPassword, "user@example.com", isPrimary: false));

            var save = async () => await context.SaveChangesAsync();
            await save.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task The_same_identifier_may_exist_under_two_different_providers()
    {
        await using var context = _database.CreateContext();
        var customerId = Guid.NewGuid();

        // The index is over (Provider, Identifier), not Identifier alone —
        // a customer whose mobile doubles as a username must still work.
        context.Add(new CustomerAuthIdentity(
            Guid.NewGuid(), customerId, AuthProviderType.MobileOtp, "+919876543210", isPrimary: true));
        context.Add(new CustomerAuthIdentity(
            Guid.NewGuid(), customerId, AuthProviderType.EmailPassword, "+919876543210", isPrimary: false));

        await context.SaveChangesAsync();

        (await context.Set<CustomerAuthIdentity>().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Two_sessions_cannot_share_a_refresh_token_hash()
    {
        const string hash = "0123456789ABCDEF";
        var now = DateTime.UtcNow;

        await using (var context = _database.CreateContext())
        {
            context.Add(new CustomerSession(Guid.NewGuid(), Guid.NewGuid(), hash, now, now.AddDays(7)));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            context.Add(new CustomerSession(Guid.NewGuid(), Guid.NewGuid(), hash, now, now.AddDays(7)));

            var save = async () => await context.SaveChangesAsync();
            await save.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task A_customer_cannot_end_up_with_two_default_addresses()
    {
        var customerId = Guid.NewGuid();

        await using (var context = _database.CreateContext())
        {
            context.Add(NewAddress(customerId, "Home", isDefault: true));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            // SRS 11.3.3: exactly one default. The partial unique index is
            // what makes a concurrent "set default" impossible to get wrong.
            context.Add(NewAddress(customerId, "Work", isDefault: true));

            var save = async () => await context.SaveChangesAsync();
            await save.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task A_customer_may_have_many_non_default_addresses()
    {
        await using var context = _database.CreateContext();
        var customerId = Guid.NewGuid();

        context.Add(NewAddress(customerId, "Home", isDefault: true));
        context.Add(NewAddress(customerId, "Work", isDefault: false));
        context.Add(NewAddress(customerId, "Parents", isDefault: false));

        await context.SaveChangesAsync();

        (await context.Set<CustomerAddress>().CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Two_customers_may_each_have_their_own_default_address()
    {
        await using var context = _database.CreateContext();

        // The partial index is scoped per customer — it must not turn into a
        // global "only one default in the whole system" rule.
        context.Add(NewAddress(Guid.NewGuid(), "Home", isDefault: true));
        context.Add(NewAddress(Guid.NewGuid(), "Home", isDefault: true));

        await context.SaveChangesAsync();

        (await context.Set<CustomerAddress>().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task A_customer_cannot_have_two_communication_preference_rows()
    {
        var customerId = Guid.NewGuid();

        await using (var context = _database.CreateContext())
        {
            context.Add(CustomerCommunicationPreference.CreateDefault(Guid.NewGuid(), customerId));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            context.Add(CustomerCommunicationPreference.CreateDefault(Guid.NewGuid(), customerId));

            var save = async () => await context.SaveChangesAsync();
            await save.Should().ThrowAsync<DbUpdateException>();
        }
    }

    private static CustomerAddress NewAddress(Guid customerId, string label, bool isDefault) =>
        new(Guid.NewGuid(), customerId, label, "12 Example Street", null, null,
            "560001", "Bengaluru", "Karnataka", 12.9716m, 77.5946m,
            "Test Customer", "+919876543210", isDefault);

    public void Dispose() => _database.Dispose();
}
