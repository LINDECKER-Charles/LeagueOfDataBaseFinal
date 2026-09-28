using LoDb.Infrastructure.Persistence.Builds;

namespace LoDb.Api.Tests.Builds.Support;

/// <summary>
/// A build row as the legacy stack wrote it, its JSON columns verbatim: valid on the latest
/// patch by default.
/// </summary>
public sealed record StoredBuild
{
    public const string ValidRunes = """
        {"primaryStyleId":8000,"primarySelections":[8005,9101,9104,8014],
        "secondaryStyleId":8100,"secondarySelections":[8126,8137]}
        """;

    public const string ValidSteps =
        """[{"label":"Start","note":null,"items":["1001","2003"]},"""
        + """{"label":"Core","note":"Rush it","items":["3078","3006"]}]""";

    public string Name { get; init; } = "Stored build";

    public string ChampionId { get; init; } = "Ahri";

    public string GameVersion { get; init; } = BuildsApp.Latest.Value;

    public string GameMode { get; init; } = "sr";

    public string Language { get; init; } = "en_US";

    public bool IsPublic { get; init; } = true;

    public string Runes { get; init; } = ValidRunes;

    public string Steps { get; init; } = ValidSteps;

    /// <summary>Whole seconds, as the legacy columns keep them.</summary>
    public DateTimeOffset CreatedAt { get; init; } = new(2025, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A token of its own, unique across the tests.</summary>
    public string ShareToken { get; init; } = Guid.NewGuid().ToString("N")[..24];

    public Build ToEntity(int ownerId) => new()
    {
        Name = Name,
        ChampionId = ChampionId,
        GameVersion = GameVersion,
        Runes = Runes,
        Steps = Steps,
        IsPublic = IsPublic,
        ShareToken = ShareToken,
        CreatedAt = CreatedAt,
        UpdatedAt = CreatedAt,
        OwnerId = ownerId,
        GameMode = GameMode,
        Language = Language,
    };
}
