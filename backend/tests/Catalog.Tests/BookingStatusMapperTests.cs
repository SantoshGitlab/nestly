using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers <see cref="BookingStatusMapper"/> (task 61's customer-visible
/// labels and task 60b's coarse buckets), which had no direct coverage
/// before task 264.
///
/// The two exhaustiveness tests below are the point of this suite: both of
/// the mapper's tables are hand-maintained dictionaries with no compiler
/// check tying them to <see cref="BookingStatus"/>, so adding an enum value
/// and forgetting a row here is silent at build time and throws
/// <see cref="KeyNotFoundException"/> at request time. That is exactly what
/// happened to <see cref="BookingStatus.Expired"/> between tasks 240 and 264
/// - <c>LabelFor</c> is called on every booking detail and list row, so an
/// expired booking 500'd its own detail page while falling out of every
/// bucket filter.
/// </summary>
public sealed class BookingStatusMapperTests
{
    public static IEnumerable<object[]> AllStatuses() =>
        Enum.GetValues<BookingStatus>().Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void Every_status_has_a_label(BookingStatus status)
    {
        var labelFor = () => BookingStatusMapper.LabelFor(status);

        labelFor.Should().NotThrow<KeyNotFoundException>();
        labelFor().Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void Every_status_has_a_bucket(BookingStatus status)
    {
        var bucketFor = () => BookingStatusMapper.BucketFor(status);

        bucketFor.Should().NotThrow<KeyNotFoundException>();
    }

    [Fact]
    public void Every_status_is_reachable_through_exactly_one_bucket_so_none_is_invisible_to_the_list_filters()
    {
        var bucketed = Enum.GetValues<BookingStatusBucket>()
            .SelectMany(BookingStatusMapper.StatusesInBucket)
            .ToList();

        bucketed.Should().OnlyHaveUniqueItems();
        bucketed.Should().BeEquivalentTo(Enum.GetValues<BookingStatus>());
    }

    [Fact]
    public void Expired_has_a_label_and_buckets_as_Cancelled()
    {
        BookingStatusMapper.LabelFor(BookingStatus.Expired).Should().Be("Expired");
        BookingStatusMapper.BucketFor(BookingStatus.Expired).Should().Be(BookingStatusBucket.Cancelled);
    }

    // --- Task 264: the tracking states ---

    [Fact]
    public void ProviderEnRoute_reads_as_On_the_way_to_the_customer()
    {
        BookingStatusMapper.LabelFor(BookingStatus.ProviderEnRoute).Should().Be("On the way");
    }

    [Fact]
    public void ProviderArrived_reads_as_Arrived_to_the_customer()
    {
        BookingStatusMapper.LabelFor(BookingStatus.ProviderArrived).Should().Be("Arrived");
    }

    [Theory]
    [InlineData(BookingStatus.ProviderEnRoute)]
    [InlineData(BookingStatus.ProviderArrived)]
    public void Both_tracking_states_bucket_as_Upcoming_alongside_Assigned_and_InProgress(BookingStatus trackingStatus)
    {
        BookingStatusMapper.BucketFor(trackingStatus).Should().Be(BookingStatusBucket.Upcoming);
        BookingStatusMapper.StatusesInBucket(BookingStatusBucket.Upcoming).Should().Contain(trackingStatus);
    }

    /// <summary>
    /// The label is what a customer literally reads while waiting, so it must
    /// stay distinct from the surrounding states - "On the way" and "Arrived"
    /// exist precisely because "Professional Assigned" and "Service in
    /// Progress" could not express either.
    /// </summary>
    [Fact]
    public void Labels_are_distinct_across_every_status()
    {
        Enum.GetValues<BookingStatus>()
            .Select(BookingStatusMapper.LabelFor)
            .Should().OnlyHaveUniqueItems();
    }
}
