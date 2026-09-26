using System.Security.Cryptography;
using System.Text;
using LoDb.Api.Modules.PublicApi.Access;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace LoDb.Api.Tests.PublicApi.Units;

/// <summary>
/// The key of a request is read where go-api reads it, and only a well-formed one reaches the
/// database, by its SHA-256.
/// </summary>
public sealed class ApiKeyCredentialTests
{
    private const string Key = "lodb_7a6835c3c34394b61bc291aa6f0029a8da1b9983";
    private const string Other = "lodb_926c7f2bd824ee97f7e1dc8fa56863b5b9633f40";

    [Fact]
    public void BearerTokenComesFirst()
    {
        var request = Request(authorization: $"Bearer {Key}", apiKey: Other);

        Assert.Equal(Key, ApiKeyCredential.Read(request));
    }

    [Theory]
    [InlineData("bearer ")]
    [InlineData("BEARER ")]
    [InlineData("Bearer")]
    [InlineData("Basic ")]
    public void AnyOtherAuthorizationFallsBackOnTheHeader(string scheme)
    {
        var request = Request(authorization: scheme + Key, apiKey: Other);

        Assert.Equal(Other, ApiKeyCredential.Read(request));
    }

    [Fact]
    public void BothAreTrimmed()
    {
        Assert.Equal(Key, ApiKeyCredential.Read(Request(authorization: $"Bearer   {Key} ")));
        Assert.Equal(Key, ApiKeyCredential.Read(Request(apiKey: $" {Key}\t")));
    }

    [Fact]
    public void AnEmptyBearerTokenIsNotReplacedByTheHeader()
    {
        var request = Request(authorization: "Bearer ", apiKey: Key);

        Assert.Equal(string.Empty, ApiKeyCredential.Read(request));
    }

    [Fact]
    public void NoKeyReadsEmpty()
    {
        Assert.Equal(string.Empty, ApiKeyCredential.Read(Request()));
    }

    [Fact]
    public void OnlyTheFirstLineOfAHeaderCounts()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[ApiKeyCredential.ApiKeyHeader] =
            StringValues.Concat(new StringValues(Key), Other);

        Assert.Equal(Key, ApiKeyCredential.Read(context.Request));
    }

    [Fact]
    public void AWellFormedKeyIsLookedUpByItsSha256()
    {
        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(Key)));

        Assert.Equal(expected, ApiKeyCredential.HashOf(Key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("lodb_7a6835c3c34394b61bc291aa6f0029a8da1b998")]
    [InlineData("lodb_7a6835c3c34394b61bc291aa6f0029a8da1b99833")]
    [InlineData("lodb_7A6835C3C34394B61BC291AA6F0029A8DA1B9983")]
    [InlineData("LODB_7a6835c3c34394b61bc291aa6f0029a8da1b9983")]
    [InlineData("lodb-7a6835c3c34394b61bc291aa6f0029a8da1b9983")]
    [InlineData("lodb_7a6835c3c34394b61bc291aa6f0029a8da1b998g")]
    [InlineData("lodb_7a6835c3c34394b61bc291aa6f0029a8da1b998 ")]
    public void AnythingElseIsMalformed(string key)
    {
        Assert.Null(ApiKeyCredential.HashOf(key));
    }

    private static HttpRequest Request(string? authorization = null, string? apiKey = null)
    {
        var context = new DefaultHttpContext();
        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        if (apiKey is not null)
        {
            context.Request.Headers[ApiKeyCredential.ApiKeyHeader] = apiKey;
        }

        return context.Request;
    }
}
