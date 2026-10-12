using LoDb.Api.Modules.PublicApi.Builds;
using Microsoft.AspNetCore.Http;

namespace LoDb.Api.Tests.PublicApi.Units;

/// <summary>
/// <c>?page=</c> and <c>?per_page=</c> read as go-api reads them (<c>strconv.Atoi</c>), then
/// the pages of a collection.
/// </summary>
public sealed class PaginationTests
{
    // Which value of the query is read, and how it is decoded, is GoQueryTests'. These rows:
    // the defaults, plain numbers, leading zeros, a sign, then the bounds.
    [Theory]
    [InlineData("", 1, 20)]
    [InlineData("?page=3&per_page=5", 3, 5)]
    [InlineData("?page=007&per_page=05", 7, 5)]
    [InlineData("?page=%2B2&per_page=%2B10", 2, 10)]
    [InlineData("?per_page=50", 1, 50)]
    [InlineData("?per_page=51", 1, 50)]
    [InlineData("?per_page=9223372036854775807", 1, 50)]
    [InlineData("?page=9223372036854775807", long.MaxValue, 20)]
    public void AValidPageIsRead(string query, long page, int perPage)
    {
        Assert.True(Pagination.TryRead(Query(query), out var pagination));
        Assert.Equal(new Pagination(page, perPage), pagination);
    }

    // Below one, for each parameter; not an integer; a space, which a bare + decodes to; and
    // one past long.MaxValue.
    [Theory]
    [InlineData("?page=0")]
    [InlineData("?per_page=0")]
    [InlineData("?page=1.5")]
    [InlineData("?page=+2")]
    [InlineData("?page=9223372036854775808")]
    public void AnythingElseIsRefused(string query)
    {
        Assert.False(Pagination.TryRead(Query(query), out _));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    public void AnEmptyCollectionStillHasAPage(long total, long pages)
    {
        Assert.Equal(pages, new Pagination(1, 5).TotalPages(total));
    }

    [Fact]
    public void APageSkipsThoseBeforeIt()
    {
        Assert.Equal(10, new Pagination(3, 5).Offset);
    }

    private static QueryString Query(string query) =>
        query.Length == 0 ? QueryString.Empty : new QueryString(query);
}
