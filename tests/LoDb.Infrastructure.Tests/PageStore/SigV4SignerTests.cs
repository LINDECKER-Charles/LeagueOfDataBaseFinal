using System.Text;
using LoDb.Infrastructure.PageStore.Signing;

namespace LoDb.Infrastructure.Tests.PageStore;

/// <summary>
/// The signer reproduces the worked examples AWS publishes for S3 (Signature Version 4,
/// header-based authentication): same signed headers, same signature.
/// </summary>
public sealed class SigV4SignerTests
{
    private const string Host = "https://examplebucket.s3.amazonaws.com";
    private const string Region = "us-east-1";

    private static readonly SigV4Credentials Example = new(
        "AKIAIOSFODNN7EXAMPLE",
        "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY");

    private static readonly DateTimeOffset SignedAt = new(2013, 5, 24, 0, 0, 0, TimeSpan.Zero);

    // "Example: GET Object": an empty payload and a Range header.
    [Fact]
    public void AGetWithARangeSignsAsTheAwsExample()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Host}/test.txt");
        request.Headers.TryAddWithoutValidation("Range", "bytes=0-9");

        new SigV4Signer(Example, Region).Sign(request, SigV4Signer.EmptyPayloadHash, SignedAt);

        AssertAuthorization(
            request,
            "host;range;x-amz-content-sha256;x-amz-date",
            "f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41");
    }

    // "Example: PUT Object": a key to escape ($), a body, a storage class and a Date header.
    [Fact]
    public void APutOfAnEscapedKeySignsAsTheAwsExample()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{Host}/test$file.text");
        request.Headers.TryAddWithoutValidation("Date", "Fri, 24 May 2013 00:00:00 GMT");
        request.Headers.TryAddWithoutValidation("x-amz-storage-class", "REDUCED_REDUNDANCY");
        var payloadHash = SigV4Signer.Hash(Encoding.UTF8.GetBytes("Welcome to Amazon S3."));

        new SigV4Signer(Example, Region).Sign(request, payloadHash, SignedAt);

        Assert.Equal(
            "44ce7dd67c959e0d3524ffac1771dfbba87d2b6b4b4e99e42034a8b803f8b072",
            Header(request, SigV4Signer.ContentHashHeader));
        AssertAuthorization(
            request,
            "date;host;x-amz-content-sha256;x-amz-date;x-amz-storage-class",
            "98ad721746da40c64f1a55b78f14c238d841ea1380cd77a1b5971af0ece108bd");
    }

    private static void AssertAuthorization(
        HttpRequestMessage request,
        string signedHeaders,
        string signature)
    {
        Assert.Equal("20130524T000000Z", Header(request, SigV4Signer.DateHeader));
        Assert.Equal(
            "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request, "
                + $"SignedHeaders={signedHeaders}, Signature={signature}",
            Header(request, "Authorization"));
    }

    private static string Header(HttpRequestMessage request, string name) =>
        request.Headers.NonValidated[name].ToString();
}
