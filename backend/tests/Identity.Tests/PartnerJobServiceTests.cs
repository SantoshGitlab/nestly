using FluentAssertions;
using Nestly.Application;
using Nestly.Application.PartnerJobs;
using Nestly.Application.PartnerManagement;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Identity.Tests;

/// <summary>
/// The partner-facing job lifecycle (task 149a, PARTNER.md API surface
/// "Jobs"), wired to the real <c>BookingPartnerAssignment</c> bridge entity
/// (task 147) rather than the earlier 501-stub JobsController.
/// </summary>
public class PartnerJobServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly Guid _partnerId;
    private readonly Guid _otherPartnerId;
    private readonly Guid _adminUserId = Guid.NewGuid();

    public PartnerJobServiceTests()
    {
        using var context = _database.CreateContext();
        var partner = new Partner(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", PartnerType.Individual, "+919876543210");
        var otherPartner = new Partner(Guid.NewGuid(), "Meena Iyer", "Meena's Services", PartnerType.Individual, "+919876500000");
        // AssignAsync (task 147) only allows assigning an Active partner.
        partner.ChangeStatus(PartnerStatus.Active);
        otherPartner.ChangeStatus(PartnerStatus.Active);
        _partnerId = partner.Id;
        _otherPartnerId = otherPartner.Id;
        context.AddRange(partner, otherPartner);
        context.SaveChanges();
    }

    private PartnerJobService CreateJobService(NestlyDbContext context) => new(
        new BookingRepository(context),
        new BookingPartnerAssignmentRepository(context),
        CreateAssignmentService(context));

    private BookingPartnerAssignmentService CreateAssignmentService(NestlyDbContext context) => new(
        new BookingRepository(context), new PartnerRepository(context), new BookingPartnerAssignmentRepository(context));

    private static Booking NewAwaitingFulfilmentBooking(Guid customerId)
    {
        var booking = new Booking(
            Guid.NewGuid(), customerId,
            new CustomerSnapshot("Asha Rao", "9876543210"),
            null,
            new AddressSnapshot("Home", "221B Baker Street", null, null, "560001", "Bengaluru", "Karnataka", 12.9716m, 77.5946m, "Asha Rao", "9876543210"),
            new SlotSnapshot(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13)),
            new PriceSnapshot(999m, 1, 999m, 0m, 0m, 999m, 0m, 0m, 0m, 999m));
        booking.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Deep Cleaning", "deep-cleaning", 999m, 1);
        booking.TransitionTo(BookingStatus.PaymentPending);
        booking.TransitionTo(BookingStatus.Confirmed);
        booking.TransitionTo(BookingStatus.AwaitingFulfilment);
        return booking;
    }

    /// <summary>Seeds a booking already Assigned to <see cref="_partnerId"/> via a real <see cref="BookingPartnerAssignmentService.AssignAsync"/> call, so the assignment row is created exactly the way task 147's admin flow creates it.</summary>
    private async Task<Guid> SeedAssignedBookingAsync(NestlyDbContext context)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        await context.AddAsync(customer);

        var booking = NewAwaitingFulfilmentBooking(customer.Id);
        await context.AddAsync(booking);
        await context.SaveChangesAsync();

        var assignResult = await CreateAssignmentService(context).AssignAsync(
            booking.Id, _adminUserId, new AssignPartnerRequest(_partnerId, ResponseDeadline: null));
        assignResult.IsSuccess.Should().BeTrue();

        return booking.Id;
    }

    [Fact]
    public async Task ListAsync_returns_every_job_ever_assigned_to_the_partner()
    {
        await using var context = _database.CreateContext();
        var bookingId = await SeedAssignedBookingAsync(context);

        var result = await CreateJobService(context).ListAsync(_partnerId, status: null, date: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.BookingId == bookingId && i.Status == PartnerJobStatus.Assigned);
    }

    [Fact]
    public async Task ListAsync_filters_by_status()
    {
        await using var context = _database.CreateContext();
        await SeedAssignedBookingAsync(context);

        var result = await CreateJobService(context).ListAsync(_partnerId, status: PartnerJobStatus.Completed, date: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDetailAsync_rejects_a_job_belonging_to_another_partner()
    {
        await using var context = _database.CreateContext();
        var bookingId = await SeedAssignedBookingAsync(context);

        var result = await CreateJobService(context).GetDetailAsync(_otherPartnerId, bookingId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PartnerJob.NotFound");
    }

    [Fact]
    public async Task AcceptAsync_moves_the_assignment_to_Accepted()
    {
        await using var context = _database.CreateContext();
        var bookingId = await SeedAssignedBookingAsync(context);

        var result = await CreateJobService(context).AcceptAsync(_partnerId, bookingId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(PartnerJobStatus.Accepted);
    }

    [Fact]
    public async Task AcceptAsync_rejects_a_caller_who_does_not_own_the_assignment()
    {
        await using var context = _database.CreateContext();
        var bookingId = await SeedAssignedBookingAsync(context);

        var result = await CreateJobService(context).AcceptAsync(_otherPartnerId, bookingId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BookingPartnerAssignment.NoOutstandingAssignment");
    }

    [Fact]
    public async Task RejectAsync_returns_the_booking_to_AwaitingFulfilment()
    {
        await using var context = _database.CreateContext();
        var bookingId = await SeedAssignedBookingAsync(context);

        var result = await CreateJobService(context).RejectAsync(_partnerId, bookingId, new RejectJobRequest("Too far away"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(PartnerJobStatus.Rejected);

        var booking = await new BookingRepository(context).GetByIdAsync(bookingId);
        booking!.Status.Should().Be(BookingStatus.AwaitingFulfilment);
        booking.AssignedPartnerId.Should().BeNull();
    }

    [Fact]
    public async Task StartAsync_requires_the_job_to_have_been_accepted_first()
    {
        await using var context = _database.CreateContext();
        var bookingId = await SeedAssignedBookingAsync(context);

        var result = await CreateJobService(context).StartAsync(_partnerId, bookingId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PartnerJob.NotFound");
    }

    [Fact]
    public async Task Full_lifecycle_accept_start_complete_and_upload_proof_succeeds()
    {
        await using var context = _database.CreateContext();
        var service = CreateJobService(context);
        var bookingId = await SeedAssignedBookingAsync(context);

        (await service.AcceptAsync(_partnerId, bookingId)).IsSuccess.Should().BeTrue();

        var started = await service.StartAsync(_partnerId, bookingId);
        started.IsSuccess.Should().BeTrue();
        started.Value.Status.Should().Be(PartnerJobStatus.InProgress);

        var completed = await service.CompleteAsync(_partnerId, bookingId);
        completed.IsSuccess.Should().BeTrue();
        completed.Value.Status.Should().Be(PartnerJobStatus.Completed);

        var proof = await service.UploadCompletionProofAsync(_partnerId, bookingId, new UploadJobCompletionProofRequest("s3://proofs/photo.jpg"));
        proof.IsSuccess.Should().BeTrue();
        proof.Value.CompletionProofRef.Should().Be("s3://proofs/photo.jpg");

        var booking = await new BookingRepository(context).GetByIdAsync(bookingId);
        booking!.Status.Should().Be(BookingStatus.Completed);
    }

    [Fact]
    public async Task CompleteAsync_requires_the_job_to_have_been_started_first()
    {
        await using var context = _database.CreateContext();
        var service = CreateJobService(context);
        var bookingId = await SeedAssignedBookingAsync(context);
        (await service.AcceptAsync(_partnerId, bookingId)).IsSuccess.Should().BeTrue();

        var result = await service.CompleteAsync(_partnerId, bookingId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PartnerJob.NotStarted");
    }

    public void Dispose() => _database.Dispose();
}
