using ArrowOut.Services.Models;

namespace ArrowOut.Tests.Services;

public class PagedResultTests
{
    [Theory]
    [InlineData(0, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(95, 10, 10)]
    public void TotalPages_IsCeiling(int total, int size, int expected)
    {
        Assert.Equal(expected, new PagedResult<int>([], 1, size, total).TotalPages);
    }

    [Fact]
    public void Page_IsClampedIntoRange()
    {
        var result = new PagedResult<int>([], 99, 10, 25);

        Assert.Equal(3, result.Page);
        Assert.True(result.HasPrevious);
        Assert.False(result.HasNext);
    }

    [Theory]
    [InlineData(0, Paging.DefaultPageSize)]
    [InlineData(-3, Paging.DefaultPageSize)]
    [InlineData(500, Paging.MaxPageSize)]
    [InlineData(20, 20)]
    public void PageSize_IsNormalised(int requested, int expected)
    {
        Assert.Equal(expected, PagedResult<int>.NormalizePageSize(requested));
    }

    [Fact]
    public void ClampPage_HandlesEmptyResults()
    {
        Assert.Equal(1, PagedResult<int>.ClampPage(7, 10, 0));
    }
}
