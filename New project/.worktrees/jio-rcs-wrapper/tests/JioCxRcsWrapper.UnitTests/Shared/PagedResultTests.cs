using FluentAssertions;
using JioCxRcsWrapper.Application.Common.Pagination;

namespace JioCxRcsWrapper.UnitTests.Shared;

public sealed class PagedResultTests
{
    [Fact]
    public void Create_ReturnsRequestedPageItemsAndMetadata()
    {
        var result = PagedResult<int>.Create(Enumerable.Range(1, 25), pageNumber: 2, pageSize: 10);

        result.Items.Should().Equal(11, 12, 13, 14, 15, 16, 17, 18, 19, 20);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalItems.Should().Be(25);
        result.TotalPages.Should().Be(3);
        result.HasPreviousPage.Should().BeTrue();
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void Create_ClampsInvalidPageNumberAndPageSize()
    {
        var result = PagedResult<int>.Create(Enumerable.Range(1, 3), pageNumber: -5, pageSize: 0);

        result.Items.Should().Equal(1, 2, 3);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalPages.Should().Be(1);
    }

    [Fact]
    public void Create_ClampsPageNumberToLastPage()
    {
        var result = PagedResult<int>.Create(Enumerable.Range(1, 12), pageNumber: 99, pageSize: 5);

        result.Items.Should().Equal(11, 12);
        result.PageNumber.Should().Be(3);
        result.TotalPages.Should().Be(3);
        result.HasNextPage.Should().BeFalse();
    }
}
