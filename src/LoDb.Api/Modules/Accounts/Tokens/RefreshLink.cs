using System.Security.Cryptography;
using System.Text;

namespace LoDb.Api.Modules.Accounts.Tokens;

/// <summary>What a refresh token stands for: its family, and its generation in it.</summary>
internal sealed record RefreshLink(Guid FamilyId, int Generation)
{
    private const int GuidBytes = 16;

    /// <summary>
    /// The link of a refresh token handed out before the families, which names none: the
    /// first generation of a family named after the token itself, so that it too serves once.
    /// </summary>
    public static RefreshLink LegacyOf(string refreshToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return new RefreshLink(new Guid(digest.AsSpan(0, GuidBytes)), 0);
    }

    /// <summary>The link of the token that follows this one.</summary>
    public RefreshLink Next => this with { Generation = Generation + 1 };
}
