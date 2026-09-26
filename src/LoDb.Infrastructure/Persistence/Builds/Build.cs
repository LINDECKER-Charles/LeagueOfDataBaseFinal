using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Infrastructure.Persistence.Builds;

/// <summary>A row of <c>builds</c>: a player's build, shared through its token.</summary>
public sealed class Build
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string ChampionId { get; set; }

    public required string GameVersion { get; set; }

    public string? Description { get; set; }

    /// <summary>JSON document (<c>jsonb</c>), kept as text until lot 5 gives it a type.</summary>
    public required string Runes { get; set; }

    /// <summary>JSON document (<c>jsonb</c>), kept as text until lot 5 gives it a type.</summary>
    public required string Steps { get; set; }

    public bool IsPublic { get; set; }

    /// <summary>24 hexadecimal characters, the <c>/b/{token}</c> of the share link.</summary>
    public required string ShareToken { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int OwnerId { get; set; }

    public User? Owner { get; set; }

    /// <summary><c>sr</c> unless the build targets another game mode.</summary>
    public required string GameMode { get; set; }

    /// <summary>Data Dragon language of the names shown with the build.</summary>
    public required string Language { get; set; }
}
