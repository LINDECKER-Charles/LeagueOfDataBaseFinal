using LoDb.Api.Modules.PublicApi.Http;
using Microsoft.AspNetCore.Http;

namespace LoDb.Api.Tests.PublicApi.Units;

/// <summary>
/// The query string read as Go's <c>url.ParseQuery</c> then <c>Values.Get</c> read it, quirks
/// the clients of go-api may rely on included.
/// </summary>
public sealed class GoQueryTests
{
    [Theory]
    [InlineData("?page=2&page=3", "2")]
    [InlineData("?Page=2", "")]
    [InlineData("?a=1&&page=7", "7")]
    [InlineData("?page", "")]
    [InlineData("?page=", "")]
    [InlineData("", "")]
    public void TheFirstValueOfTheExactNameWins(string query, string expected)
    {
        Assert.Equal(expected, Get(query));
    }

    [Theory]
    [InlineData("?page=a+b", "a b")]
    [InlineData("?page=%41%62", "Ab")]
    [InlineData("?page=%2B2", "+2")]
    [InlineData("?page=%C3%A9t%C3%A9", "été")]
    [InlineData("?%70age=6", "6")]
    [InlineData("?pa+ge=1&page=2", "2")]
    public void NamesAndValuesAreUnescaped(string query, string expected)
    {
        Assert.Equal(expected, Get(query));
    }

    [Theory]
    [InlineData("?page=1;x&page=4", "4")]
    [InlineData("?x=1;page=2&page=5", "5")]
    [InlineData("?page=%zz&page=5", "5")]
    [InlineData("?page=%4&page=6", "6")]
    [InlineData("?page=%", "")]
    [InlineData("?pa%ge=1&page=7", "7")]
    public void APairGoRefusesIsSkipped(string query, string expected)
    {
        Assert.Equal(expected, Get(query));
    }

    private static string Get(string query) =>
        GoQuery.Get(query.Length == 0 ? QueryString.Empty : new QueryString(query), "page");
}
