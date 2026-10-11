namespace LoDb.Api.Modules.Catalog.WarmUp.Progress;

/// <summary>Where a warm-up stands; the last two end the stream.</summary>
internal enum WarmUpStage
{
    /// <summary>The datasets of the version and the language are being ingested.</summary>
    Preparing,

    /// <summary>The images the lists show are being fetched.</summary>
    Images,

    /// <summary>
    /// The catalog is open. Images short of the total were refused for now (budget spent, no
    /// slot): they stay pending, for the pages to fill as they arrive.
    /// </summary>
    Done,

    /// <summary>
    /// The run was cut short, its counts kept: the catalog could not be opened, Data Dragon
    /// failed or the stream outlived its lifetime. <c>failure</c> tells why.
    /// </summary>
    Failed,
}
