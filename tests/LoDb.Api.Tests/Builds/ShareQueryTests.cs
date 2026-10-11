using LoDb.Api.Modules.Builds.Sharing;
using Microsoft.AspNetCore.Http;

namespace LoDb.Api.Tests.Builds;

/// <summary>
/// The query of a shared build is a 400 when its version or its language is malformed, the
/// version reported first; an unset field is never malformed.
/// </summary>
public sealed class ShareQueryTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("16.19.1", "fr_FR", null)]
    [InlineData("seven", null, "invalid-version")]
    [InlineData(null, "zz", "invalid-language")]
    [InlineData("seven", "zz", "invalid-version")]
    public void MalformedNamesTheFirstUnusableField(string? version, string? lang, string? code)
    {
        var query = new ShareQuery(version, lang, new DefaultHttpContext());

        Assert.Equal(code, query.Malformed?.Code);
    }
}
