using System.Buffers.Text;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.ClientPolicy.Versions;
using LoDb.Infrastructure.Persistence.Apps;

namespace LoDb.Api.Modules.ClientPolicy.Publishing;

/// <summary>
/// Checks a publication before anything is written, with the rules of the table and of
/// ADR 0008, so that a refused one says which field is wrong rather than failing on a
/// constraint.
/// </summary>
internal static class PublishPolicyRules
{
    public const string MinimumField = "minimumVersion";
    public const string LatestField = "latestVersion";
    public const string BundleField = "bundle";

    private const int ChecksumLength = 64;

    // Every field of a bundle is required; each has its own format.
    private static readonly BundleRule[] BundleRules =
    [
        new("bundle.id", static b => b.Id, IsBundleId, PolicyErrors.InvalidId),
        new("bundle.url", static b => b.Url, IsHttpsUrl, PolicyErrors.InvalidUrl),
        new("bundle.checksum", static b => b.Checksum, IsChecksum, PolicyErrors.InvalidChecksum),
        new(
            "bundle.signature",
            static b => b.Signature,
            IsSignature,
            PolicyErrors.InvalidSignature),
        new(
            "bundle.minimumNativeVersion",
            static b => b.MinimumNativeVersion,
            static text => VersionOf(text) is not null,
            PolicyErrors.InvalidVersion),
    ];

    /// <summary>The codes of the invalid fields; empty when the publication holds.</summary>
    public static FieldErrors Check(ClientPlatform platform, PublishPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new FieldErrors();
        var minimum = CheckVersion(errors, MinimumField, request.MinimumVersion);
        var latest = CheckVersion(errors, LatestField, request.LatestVersion);
        if (minimum is not null && latest is not null && minimum > latest)
        {
            errors.Add(MinimumField, PolicyErrors.AboveLatest);
        }

        if (request.Bundle is not { } bundle)
        {
            return errors;
        }

        if (platform != ClientPlatform.Android)
        {
            errors.Add(BundleField, PolicyErrors.BundleNotSupported);
            return errors;
        }

        CheckBundle(errors, bundle);
        return errors;
    }

    private static void CheckBundle(FieldErrors errors, BundleRequest bundle)
    {
        foreach (var rule in BundleRules)
        {
            var value = rule.Value(bundle);
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add(rule.Field, FieldErrors.Required);
            }
            else if (!rule.IsValid(value))
            {
                errors.Add(rule.Field, rule.Code);
            }
        }
    }

    // Null stands for "no such version", which the policy allows.
    private static Version? CheckVersion(FieldErrors errors, string field, string? text)
    {
        if (text is null)
        {
            return null;
        }

        var version = VersionOf(text);
        if (version is null)
        {
            errors.Add(field, PolicyErrors.InvalidVersion);
        }

        return version;
    }

    private static Version? VersionOf(string text) =>
        text.Length <= ClientPolicyEntry.VersionMaxLength && AppVersion.TryParse(text, out var v)
            ? v
            : null;

    private static bool IsBundleId(string id) =>
        id.Length <= ClientPolicyEntry.BundleIdMaxLength
        && id.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

    private static bool IsHttpsUrl(string url) =>
        url.Length <= ClientPolicyEntry.BundleUrlMaxLength
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;

    private static bool IsChecksum(string checksum) =>
        checksum.Length == ChecksumLength && checksum.All(char.IsAsciiHexDigitLower);

    // Base64.IsValid lets blanks through, which the plugin would not decode.
    private static bool IsSignature(string signature) =>
        signature.Length <= ClientPolicyEntry.BundleSignatureMaxLength
        && !signature.Any(char.IsWhiteSpace)
        && Base64.IsValid(signature);

    private sealed record BundleRule(
        string Field,
        Func<BundleRequest, string?> Value,
        Func<string, bool> IsValid,
        string Code);
}
