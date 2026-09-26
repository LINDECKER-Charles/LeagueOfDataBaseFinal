namespace LoDb.Infrastructure.Storage.Datasets;

/// <summary>
/// Address of a dataset: <c>data/{version}/{lang}/{type}.json</c>.
/// </summary>
/// <remarks>
/// The store does not know the dataset types, the normalization defines them. Each part is
/// validated at construction: a segment starts with a letter or a digit and holds only
/// letters, digits, <c>.</c>, <c>_</c> and <c>-</c>, so a key can neither climb out of the
/// data directory nor be absolute.
/// </remarks>
public sealed record DatasetKey
{
    /// <param name="version">Data Dragon version, such as <c>14.1.1</c>.</param>
    /// <param name="language">Data Dragon language, such as <c>en_US</c>.</param>
    /// <param name="type">
    /// Dataset type, one or more <c>/</c>-separated segments without extension, such as
    /// <c>champion</c> or <c>championDetail/Aatrox</c>.
    /// </param>
    /// <exception cref="ArgumentException">A part is not a valid path.</exception>
    public DatasetKey(string version, string language, string type)
    {
        Version = StoragePathSegments.Require(version, nameof(version));
        Language = StoragePathSegments.Require(language, nameof(language));
        Type = StoragePathSegments.RequireRelativePath(type, nameof(type));
    }

    public string Version { get; }

    public string Language { get; }

    public string Type { get; }

    /// <summary><c>data/{version}/{lang}/{type}.json</c>, relative to the storage root.</summary>
    public string RelativePath =>
        $"{StorageLayout.DataDirectory}/{Version}/{Language}/{Type}"
        + StorageLayout.DatasetExtension;
}
