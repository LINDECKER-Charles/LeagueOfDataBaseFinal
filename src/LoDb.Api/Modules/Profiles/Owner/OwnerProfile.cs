using LoDb.Api.Modules.Profiles.Deletion;
using LoDb.Api.Modules.Profiles.Showcase;

namespace LoDb.Api.Modules.Profiles.Owner;

/// <summary>The profile as its owner manages it, <c>/{locale}/account/profile</c>.</summary>
internal sealed record OwnerProfile
{
    public required string Username { get; init; }

    /// <summary>The Riot tag line shown after the username, such as <c>EUW</c>.</summary>
    public required string? RiotTagline { get; init; }

    /// <summary>The e-mail with its local part masked, such as <c>c***@outlook.fr</c>.</summary>
    public required string MaskedEmail { get; init; }

    public required DateTimeOffset MemberSince { get; init; }

    /// <summary>False for an account created through Google, which may then set one.</summary>
    public required bool HasPassword { get; init; }

    /// <summary>What the account types to confirm its erasure.</summary>
    public required DeletionConfirmation DeletionConfirmation { get; init; }

    /// <summary>Whether the public card, <c>/u/{username}</c>, is shown to visitors.</summary>
    public required bool IsPublic { get; init; }

    /// <summary>The version the favorites are pinned to; null to follow the one browsed.</summary>
    public required string? PreferredVersion { get; init; }

    public required ProfileShowcase Showcase { get; init; }

    /// <summary>
    /// The favorite champion's art, never the skin: the skin is what the public card shows off.
    /// </summary>
    public required ProfileBackdrop? Backdrop { get; init; }
}
