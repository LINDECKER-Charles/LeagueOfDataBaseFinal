using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Images;

namespace LoDb.Ingestion.Queue;

/// <summary>
/// The keys of the units of on-demand work: the four datasets of a (version, language), or
/// one image of a version. Identical work has the same key, whoever asks for it.
/// </summary>
internal static class OnDemandUnits
{
    public static string Dataset(PatchVersion version, DdragonLanguage language) =>
        $"datasets|{version.Value}|{language.Code}";

    public static string Image(PatchVersion version, DdragonImage image) =>
        $"image|{version.Value}|{image.ManifestType}|{image.ManifestKey}";

    /// <summary>The units a queued request covers, each once.</summary>
    public static List<string> Of(OnDemandRequest request)
    {
        List<string> units =
            [.. request.Images.Select(image => Image(request.Version, image)).Distinct()];
        if (request.Language is { } language)
        {
            units.Add(Dataset(request.Version, language));
        }

        return units;
    }
}
