using FluentAssertions;
using Nestly.Application;
using Nestly.Application.Bookings;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 198 - customer/admin read access to a booking's completion proof.</summary>
public sealed class BookingCompletionProofSupportTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public BookingCompletionProofSupportTests(TestDatabase db) => _db = db;

    private static Booking SeedBooking(Nestly.Infrastructure.Persistence.NestlyDbContext context, Guid customerId)
    {
        var customer = new Customer(customerId, "9" + Guid.NewGuid().ToString("N")[..9], "Test Customer", CustomerStatus.Active);
        context.Add(customer);

        var booking = new Booking(
            Guid.NewGuid(), customerId,
            new CustomerSnapshot("Test Customer", customer.Mobile),
            null,
            new AddressSnapshot("Home", "123 St", null, null, "560001", "Bengaluru", "Karnataka", 12.9m, 77.5m, "Test", "9000000000"),
            new SlotSnapshot(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13)),
            new PriceSnapshot(500m, 1, 500m, 0, 0, 500m, 0, 0, 0, 500m));

        context.Add(booking);
        context.SaveChanges();
        return booking;
    }

    [Fact]
    public async Task GetForCustomerAsync_returns_the_proof_for_the_bookings_own_customer()
    {
        using var context = _db.CreateContext();
        var customerId = Guid.NewGuid();
        var booking = SeedBooking(context, customerId);
        var proof = new BookingCompletionProof(Guid.NewGuid(), booking.Id, Guid.NewGuid(), ["s3://proofs/a.jpg"], []);
        context.Add(proof);
        context.SaveChanges();

        var completionProofRepository = new BookingCompletionProofRepository(context);
        var bookingRepository = new BookingRepository(context);

        var result = await completionProofRepository.GetForCustomerAsync(bookingRepository, customerId, booking.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PhotoRefs.Should().ContainSingle(r => r == "s3://proofs/a.jpg");
    }

    [Fact]
    public async Task GetForCustomerAsync_returns_null_when_no_proof_exists_yet()
    {
        using var context = _db.CreateContext();
        var customerId = Guid.NewGuid();
        var booking = SeedBooking(context, customerId);

        var result = await new BookingCompletionProofRepository(context).GetForCustomerAsync(new BookingRepository(context), customerId, booking.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task GetForCustomerAsync_hides_the_booking_from_a_non_owning_customer()
    {
        using var context = _db.CreateContext();
        var booking = SeedBooking(context, Guid.NewGuid());

        var result = await new BookingCompletionProofRepository(context).GetForCustomerAsync(new BookingRepository(context), Guid.NewGuid(), booking.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.NotFound");
    }

    [Fact]
    public async Task GetForAdminAsync_returns_the_proof_regardless_of_which_customer_owns_the_booking()
    {
        using var context = _db.CreateContext();
        var booking = SeedBooking(context, Guid.NewGuid());
        var proof = new BookingCompletionProof(Guid.NewGuid(), booking.Id, Guid.NewGuid(), ["s3://proofs/b.jpg"], []);
        context.Add(proof);
        context.SaveChanges();

        var result = await new BookingCompletionProofRepository(context).GetForAdminAsync(new BookingRepository(context), booking.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetForAdminAsync_404s_for_a_booking_that_does_not_exist()
    {
        using var context = _db.CreateContext();

        var result = await new BookingCompletionProofRepository(context).GetForAdminAsync(new BookingRepository(context), Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.NotFound");
    }
}
