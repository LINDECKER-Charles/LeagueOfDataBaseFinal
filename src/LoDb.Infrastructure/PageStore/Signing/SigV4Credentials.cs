namespace LoDb.Infrastructure.PageStore.Signing;

/// <summary>
/// An access key and its secret, as an object store's IAM application hands them out.
/// </summary>
/// <remarks>
/// A class rather than a record on purpose: a record would print the secret in its
/// <c>ToString</c>, and from there in any log or exception that formats it.
/// </remarks>
internal sealed class SigV4Credentials(string accessKey, string secretKey)
{
    public string AccessKey { get; } = accessKey;

    public string SecretKey { get; } = secretKey;
}
