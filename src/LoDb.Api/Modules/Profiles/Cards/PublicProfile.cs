using LoDb.Api.Modules.Profiles.Cards.Builds;
using LoDb.Api.Modules.Profiles.Showcase;

namespace LoDb.Api.Modules.Profiles.Cards;

/// <summary>
/// The public card of a profile, <c>/u/{username}</c>: the same answer for the visitors and
/// for the owner's preview, so the preview is the page, not an approximation.
/// </summary>
internal sealed record PublicProfile
{
    public required string Username { get; init; }

    /// <summary>The Riot tag line shown after the username, such as <c>EUW</c>.</summary>
    public required string? RiotTagline { get; init; }

    /// <summary>Whether the account supports the site, which the card shows with a badge.</summary>
    public required bool IsSupporter { get; init; }

    /// <summary>Always true for a visitor; false on the preview of a private profile.</summary>
    public required bool IsPublic { get; init; }

    public required DateTimeOffset MemberSince { get; init; }

    public required ProfileShowcase Showcase { get; init; }

    /// <summary>The favorite skin, else the favorite champion; null for neither.</summary>
    public required ProfileBackdrop? Backdrop { get; init; }

    /// <summary>
    /// The builds the owner published, most recently updated first; empty until the builds
    /// module plugs its source in.
    /// </summary>
    public required IReadOnlyList<ProfileBuildCard> Builds { get; init; }
}
