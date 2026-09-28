using LoDb.Api.Modules.Billing.Keys;

namespace LoDb.Api.Modules.PublicApi.Keys;

/// <summary>The name of a key as stored: trimmed, 64 characters at most, never blank.</summary>
internal static class KeyNames
{
    /// <summary>The length of <c>api_keys.name</c>.</summary>
    public const int MaxLength = 64;

    /// <summary>
    /// <paramref name="name"/> trimmed and cut, as the legacy issuer does, without splitting
    /// a surrogate pair; <c>default</c> when nothing is left.
    /// </summary>
    public static string Normalize(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length > MaxLength)
        {
            var cut = char.IsHighSurrogate(trimmed[MaxLength - 1]) ? MaxLength - 1 : MaxLength;
            trimmed = trimmed[..cut].TrimEnd();
        }

        return trimmed.Length == 0 ? ApiKeySecrets.DefaultName : trimmed;
    }
}
