namespace LoDb.Domain.Languages;

/// <summary>
/// Order in which languages are tried when a version lacks the requested one.
/// </summary>
/// <remarks>
/// Old versions miss whole languages or files for some of them (UP 1): the requested
/// language is tried first, then <c>en_US</c>, the only language every version ships (UP 2).
/// When neither is there, the answer is an empty dataset, never an error page.
/// </remarks>
public static class LanguageFallback
{
    /// <summary>Languages to try, in order, without duplicates.</summary>
    public static IReadOnlyList<DdragonLanguage> Chain(DdragonLanguage requested)
    {
        ArgumentNullException.ThrowIfNull(requested);
        return requested == DdragonLanguage.EnUs ? [requested] : [requested, DdragonLanguage.EnUs];
    }

    /// <summary>
    /// First language of the chain whose data is present, or <see langword="null"/> when the
    /// caller must serve an empty dataset.
    /// </summary>
    public static DdragonLanguage? FirstPresent(
        DdragonLanguage requested,
        Func<DdragonLanguage, bool> isPresent)
    {
        ArgumentNullException.ThrowIfNull(isPresent);
        return Chain(requested).FirstOrDefault(isPresent);
    }
}
