using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Api.Modules.Profiles.Favorites;

/// <summary>The ids of the four favorite slots, as stored or as submitted.</summary>
internal sealed record FavoriteIds
{
    // Lengths of the users.favorite_*_id columns.
    private const int NameIdMaxLength = 64;
    private const int NumericIdMaxLength = 16;

    public static FavoriteIds None { get; } = new();

    public string? Champion { get; init; }

    public string? Item { get; init; }

    public string? Rune { get; init; }

    public string? Summoner { get; init; }

    public static FavoriteIds Of(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new FavoriteIds
        {
            Champion = user.FavoriteChampionId,
            Item = user.FavoriteItemId,
            Rune = user.FavoriteRuneId,
            Summoner = user.FavoriteSummonerId,
        };
    }

    /// <summary>The longest id the column of <paramref name="slot"/> holds.</summary>
    public static int MaxLength(FavoriteSlot slot) => slot switch
    {
        FavoriteSlot.Item or FavoriteSlot.Rune => NumericIdMaxLength,
        _ => NameIdMaxLength,
    };

    public string? Get(FavoriteSlot slot) => slot switch
    {
        FavoriteSlot.Champion => Champion,
        FavoriteSlot.Item => Item,
        FavoriteSlot.Rune => Rune,
        FavoriteSlot.Summoner => Summoner,
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };

    public FavoriteIds With(FavoriteSlot slot, string? id) => slot switch
    {
        FavoriteSlot.Champion => this with { Champion = id },
        FavoriteSlot.Item => this with { Item = id },
        FavoriteSlot.Rune => this with { Rune = id },
        FavoriteSlot.Summoner => this with { Summoner = id },
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };

    public void ApplyTo(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.FavoriteChampionId = Champion;
        user.FavoriteItemId = Item;
        user.FavoriteRuneId = Rune;
        user.FavoriteSummonerId = Summoner;
    }
}
