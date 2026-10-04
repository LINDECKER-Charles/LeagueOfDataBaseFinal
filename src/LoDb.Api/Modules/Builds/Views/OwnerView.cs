using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Api.Modules.Builds.Views;

/// <summary>The author of a build, as a shared page or the trends credit them.</summary>
internal sealed record OwnerView
{
    public required string Username { get; init; }

    public string? RiotTagline { get; init; }

    /// <summary>Whether the author supports the site: the badge beside their name.</summary>
    public required bool IsSupporter { get; init; }

    /// <summary>Whether their name links to a public profile that answers.</summary>
    public required bool HasPublicProfile { get; init; }

    public static OwnerView Of(User owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return new OwnerView
        {
            Username = owner.UserName ?? string.Empty,
            RiotTagline = owner.RiotTagline,
            IsSupporter = owner.IsSupporter,
            HasPublicProfile = owner.IsPublicProfile && !owner.IsBanned,
        };
    }
}
