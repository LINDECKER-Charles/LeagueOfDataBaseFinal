namespace LoDb.Api.Modules.Admin.Storage.Views;

/// <summary>The datasets stored for one version.</summary>
internal sealed record VersionCoverage
{
    public required string Version { get; init; }

    /// <summary>Its languages, sorted.</summary>
    public required IReadOnlyList<string> Langs { get; init; }

    /// <summary>Its dataset types, sorted.</summary>
    public required IReadOnlyList<string> Types { get; init; }

    public required long Objects { get; init; }
}
