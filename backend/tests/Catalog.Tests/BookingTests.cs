using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 55a-d (booking schema) and 56a-d (lifecycle enum, transition matrix, status history, immutability).</summary>
public sealed class BookingTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public BookingTests(TestDatabase db) => _db = db;

    private static Customer NewCustomer() =>
        new(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active, "asha@example.com");

    private static CustomerSnapshot CustomerSnapshotFor(Customer customer) => new(customer.Name, customer.Mobile);

    private static readonly AddressSnapshot Address = new(
        "Home", "221B Baker Street", null, "Near the park", "560001", "Bengaluru", "Karnataka", 12.9716m, 77.5946m, "Asha Rao", "9876543210");

    private static readonly SlotSnapshot Slot = new(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13));

    private static readonly PriceSnapshot Price = new(
        BasePrice: 500m, Quantity: 1, BaseTotal: 500m, AddOnTotal: 0m, VisitCharge: 50m,
        Subtotal: 550m, TaxPercentage: 18m, TaxAmount: 99m, PlatformFee: 10m, TotalPayable: 659m);

    private static Booking NewBooking(Customer customer, Guid? sourceAddressId = null) =>
        new(Guid.NewGuid(), customer.Id, CustomerSnapshotFor(customer), sourceAddressId, Address, Slot, Price);

    [Fact]
    public void Creating_a_booking_starts_it_Initiated_with_one_status_history_row_and_raises_BookingCreatedEvent()
    {
        var customer = NewCustomer();
        var booking = NewBooking(customer);

        booking.Status.Should().Be(BookingStatus.Initiated);
        booking.StatusHistory.Should().ContainSingle();
        booking.StatusHistory[0].FromStatus.Should().BeNull();
        booking.StatusHistory[0].ToStatus.Should().Be(BookingStatus.Initiated);
        booking.DomainEvents.Should().ContainSingle(e => e is Nestly.Domain.Events.BookingCreatedEvent);
    }

    [Fact]
    public void Snapshot_fields_are_copied_from_the_supplied_snapshots_exactly()
    {
        var customer = NewCustomer();
        var booking = NewBooking(customer);

        booking.CustomerNameSnapshot.Should().Be(customer.Name);
        booking.CustomerMobileSnapshot.Should().Be(customer.Mobile);
        booking.AddressLine1Snapshot.Should().Be(Address.Line1);
        booking.AddressPincodeSnapshot.Should().Be(Address.Pincode);
        booking.SlotWindowId.Should().Be(Slot.SlotWindowId);
        booking.SlotDate.Should().Be(Slot.Date);
        booking.TotalPayableSnapshot.Should().Be(Price.TotalPayable);
    }

    [Fact]
    public void AddItem_and_AddAddOn_succeed_while_the_booking_is_still_Initiated()
    {
        var customer = NewCustomer();
        var booking = NewBooking(customer);

        var item = booking.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Deep Clean", "deep-clean", 500m, 1);
        item.AddAddOn(Guid.NewGuid(), Guid.NewGuid(), "Sofa Cleaning", 150m, 2);

        booking.Items.Should().ContainSingle();
        booking.Items[0].AddOns.Should().ContainSingle(a => a.LineTotalSnapshot == 300m);
    }

    [Fact]
    public void AddItem_is_rejected_once_the_booking_has_moved_past_Initiated()
    {
        var customer = NewCustomer();
        var booking = NewBooking(customer);
        booking.TransitionTo(BookingStatus.PaymentPending);

        var act = () => booking.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Deep Clean", "deep-clean", 500m, 1);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_legal_transition_updates_status_and_appends_a_history_row_with_the_right_from_and_to()
    {
        var customer = NewCustomer();
        var booking = NewBooking(customer);

        booking.TransitionTo(BookingStatus.PaymentPending);

        booking.Status.Should().Be(BookingStatus.PaymentPending);
        booking.StatusHistory.Should().HaveCount(2);
        booking.StatusHistory[1].FromStatus.Should().Be(BookingStatus.Initiated);
        booking.StatusHistory[1].ToStatus.Should().Be(BookingStatus.PaymentPending);
    }

    [Fact]
    public void An_illegal_transition_is_rejected_and_leaves_status_unchanged()
    {
        var customer = NewCustomer();
        var booking = NewBooking(customer);

        var act = () => booking.TransitionTo(BookingStatus.Completed);

        act.Should().Throw<InvalidOperationException>();
        booking.Status.Should().Be(BookingStatus.Initiated);
        booking.StatusHistory.Should().ContainSingle();
    }

    [Fact]
    public void Refunded_is_a_terminal_state_with_no_legal_transitions_out()
    {
        BookingLifecycle.IsTerminal(BookingStatus.Refunded).Should().BeTrue();
        BookingLifecycle.IsValidTransition(BookingStatus.Refunded, BookingStatus.Initiated).Should().BeFalse();
    }

    [Fact]
    public void Initiated_is_not_terminal()
    {
        BookingLifecycle.IsTerminal(BookingStatus.Initiated).Should().BeFalse();
    }

    [Fact]
    public void Booking_with_items_addons_and_status_history_round_trips_through_the_database()
    {
        var customer = NewCustomer();
        var booking = NewBooking(customer);
        var item = booking.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Deep Clean", "deep-clean", 500m, 1);
        item.AddAddOn(Guid.NewGuid(), Guid.NewGuid(), "Sofa Cleaning", 150m, 2);
        booking.TransitionTo(BookingStatus.PaymentPending);
        booking.TransitionTo(BookingStatus.Confirmed);

        using (var context = _db.CreateContext())
        {
            context.Add(customer);
            context.Add(booking);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var loaded = readContext.Bookings
            .Include(b => b.Items).ThenInclude(i => i.AddOns)
            .Include(b => b.StatusHistory)
            .Single(b => b.Id == booking.Id);

        loaded.Status.Should().Be(BookingStatus.Confirmed);
        loaded.StatusHistory.Should().HaveCount(3);
        loaded.Items.Should().ContainSingle();
        loaded.Items[0].AddOns.Should().ContainSingle(a => a.NameSnapshot == "Sofa Cleaning");
        loaded.TotalPayableSnapshot.Should().Be(659m);
    }
}
