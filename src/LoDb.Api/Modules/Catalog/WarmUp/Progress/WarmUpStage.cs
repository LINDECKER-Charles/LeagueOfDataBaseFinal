namespace LoDb.Api.Modules.Catalog.WarmUp.Progress;

/// <summary>Where a warm-up stands; the last two end the stream.</summary>
internal enum WarmUpStage
{
    /// <summary>The datasets of the version and the language are being ingested.</summary>
    Preparing,

    /// <summary>The images the lists show are being fetched.</summary>
    Images,

    Done,

    /// <summary>The catalog could not be opened: <c>failure</c> tells why.</summary>
    Failed,
}
