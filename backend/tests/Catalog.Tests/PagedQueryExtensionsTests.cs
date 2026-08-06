using FluentAssertions;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Tasks 261/262: guards the shared paging helper that replaced the
/// hand-written <c>.Skip((page - 1) * pageSize)</c> in all 14 paged query
/// sites.
///
/// The defect it fixes: that expression is unchecked <see cref="int"/>
/// arithmetic, and the admin paging validators bound <c>PageSize</c> to
/// 1..100 but bound <c>Page</c> only with <c>GreaterThanOrEqualTo(1)</c>. So
/// <c>Page=2000000000&amp;PageSize=100</c> passed validation, wrapped to
/// -1863463012, and reached PostgreSQL as a negative OFFSET - rejected with
/// "OFFSET must not be negative", i.e. an unhandled 500 on every paged admin
/// screen.
///
/// These assert on the computed offset rather than on query behaviour on
/// purpose: the test databases are in-memory SQLite, which silently treats a
/// negative OFFSET as 0 where PostgreSQL errors, so a query-level test would
/// pass against SQLite even with the bug still present.
/// </summary>
public class PagedQueryExtensionsTests
{
    [Theory]
    [InlineData(2_000_000_000, 100)]
    [InlineData(int.MaxValue, 100)]
    [InlineData(int.MaxValue, 20)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void Offset_SaturatesInsteadOfWrapping_ForAbsurdPageNumbers(int page, int pageSize)
    {
        // The exact pre-fix expression, to document what used to happen.
        int wrapped = unchecked((page - 1) * pageSize);

        int offset = PagedQueryExtensions.Offset(page, pageSize);

        offset.Should().BeGreaterThanOrEqualTo(0, "a negative OFFSET is a hard error in PostgreSQL");
        if (wrapped < 0)
        {
            offset.Should().NotBe(wrapped);
        }
    }

    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(2, 20, 20)]
    [InlineData(3, 50, 100)]
    [InlineData(10, 100, 900)]
    public void Offset_IsUnchangedForOrdinaryPages(int page, int pageSize, int expected)
    {
        // Regression guard: the overflow fix must not shift ordinary paging
        // by even one row.
        PagedQueryExtensions.Offset(page, pageSize).Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Offset_IsNeverNegative_ForNonPositivePages(int page)
    {
        PagedQueryExtensions.Offset(page, 20).Should().Be(0);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(int.MinValue, 1)]
    public void Normalize_CoercesNonPositivePagesToTheFirstPage(int page, int expected)
    {
        PagedQueryExtensions.Normalize(page, 20).Page.Should().Be(expected);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(10_000)]
    [InlineData(int.MaxValue)]
    public void Normalize_CapsPageSizeAtTheMaximum(int pageSize)
    {
        PagedQueryExtensions.Normalize(1, pageSize).PageSize
            .Should().Be(PagedQueryExtensions.MaxPageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Normalize_SubstitutesTheDefaultForNonPositivePageSizes(int pageSize)
    {
        PagedQueryExtensions.Normalize(1, pageSize).PageSize
            .Should().Be(PagedQueryExtensions.DefaultPageSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(100)]
    public void Normalize_LeavesInRangePageSizesAlone(int pageSize)
    {
        PagedQueryExtensions.Normalize(1, pageSize).PageSize.Should().Be(pageSize);
    }

    [Fact]
    public void ApplyPaging_CapsTheNumberOfRowsReturned()
    {
        IQueryable<int> rows = Enumerable.Range(1, 500).AsQueryable();

        // A caller asking for 10,000 rows gets the cap, not the table.
        rows.ApplyPaging(1, 10_000).Should().HaveCount(PagedQueryExtensions.MaxPageSize);
    }

    [Fact]
    public void ApplyPaging_ReturnsTheExpectedWindowForAnOrdinaryPage()
    {
        IQueryable<int> rows = Enumerable.Range(1, 500).AsQueryable();

        rows.ApplyPaging(3, 50).Should().Equal(Enumerable.Range(101, 50));
    }

    [Fact]
    public void ApplyPaging_ReturnsAnEmptyPageWellPastTheEnd_RatherThanThrowing()
    {
        IQueryable<int> rows = Enumerable.Range(1, 500).AsQueryable();

        rows.ApplyPaging(int.MaxValue, 100).Should().BeEmpty();
    }
}
