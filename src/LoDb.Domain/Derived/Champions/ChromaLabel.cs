using System.Globalization;
using System.Text.RegularExpressions;
using LoDb.Domain.Catalog.Champions;

namespace LoDb.Domain.Derived.Champions;

/// <summary>
/// Color label of a chroma, derived from its accent hue (UP 9).
/// </summary>
/// <remarks>
/// CommunityDragon rarely names a variant (a chroma's name is usually its base skin's), so the
/// label describes the actual color instead of claiming a Riot product name. A parenthetical
/// suffix, when a patch provides one ("… (Ruby)"), always wins. Labels are English, as on the
/// legacy site.
/// </remarks>
public static partial class ChromaLabel
{
    /// <summary>Label of a chroma whose color cannot be read.</summary>
    public const string Unknown = "Chroma";

    private const string Obsidian = "Obsidian";
    private const string Pearl = "Pearl";
    private const string Steel = "Steel";
    private const char HexMarker = '#';
    private const int VariantGroup = 1;
    private const int RedGroup = 1;
    private const int GreenGroup = 2;
    private const int BlueGroup = 3;

    // Below this absolute chroma a swatch reads as grey, whatever its hue.
    private const double AchromaticMaxChroma = 0.1;
    private const double GreyPearlMinLightness = 0.75;
    private const double GreyObsidianMaxLightness = 0.2;

    // Colored swatches this dark or this bright lose their hue to the eye.
    private const double ObsidianMaxLightness = 0.15;
    private const double PearlMinLightness = 0.92;

    // Upper hue bound of each named bucket, in degrees, inclusive and ascending.
    private static readonly IReadOnlyList<(double Ceiling, string Label)> HueBuckets =
    [
        (15, "Crimson"), (40, "Amber"), (65, "Gold"), (150, "Emerald"), (195, "Teal"),
        (240, "Azure"), (280, "Sapphire"), (320, "Violet"), (345, "Rose"), (360, "Crimson"),
    ];

    public static string Of(Chroma chroma)
    {
        ArgumentNullException.ThrowIfNull(chroma);
        var suffix = ParentheticalSuffix().Match(chroma.Name);
        if (suffix.Success)
        {
            return suffix.Groups[VariantGroup].Value;
        }

        return ColorName(chroma.Colors.Count > 0 ? chroma.Colors[0] : null);
    }

    private static string ColorName(string? hex)
    {
        if (Rgb.Read(hex) is not { } accent)
        {
            return Unknown;
        }

        if (accent.Chroma < AchromaticMaxChroma)
        {
            return GreyName(accent.Lightness);
        }

        if (accent.Lightness < ObsidianMaxLightness)
        {
            return Obsidian;
        }

        return accent.Lightness > PearlMinLightness ? Pearl : HueName(accent.Hue);
    }

    private static string GreyName(double lightness)
    {
        if (lightness > GreyPearlMinLightness)
        {
            return Pearl;
        }

        return lightness < GreyObsidianMaxLightness ? Obsidian : Steel;
    }

    private static string HueName(double hue)
    {
        foreach (var (ceiling, label) in HueBuckets)
        {
            if (hue <= ceiling)
            {
                return label;
            }
        }

        return Unknown;
    }

    [GeneratedRegex(@"\(([^)]+)\)\s*\z")]
    private static partial Regex ParentheticalSuffix();

    [GeneratedRegex(@"^([0-9a-fA-F]{2})([0-9a-fA-F]{2})([0-9a-fA-F]{2})\z")]
    private static partial Regex HexTriplet();

    // Channels in [0, 1]. The absolute chroma (max - min) stays reliable near black and white,
    // unlike HSL saturation.
    private readonly record struct Rgb(double Red, double Green, double Blue)
    {
        private const double ChannelMax = 255;
        private const double DegreesPerSextant = 60;
        private const double FullTurn = 360;
        private const double Sextants = 6;
        private const double GreenSextant = 2;
        private const double BlueSextant = 4;

        public double Chroma => Max - Min;

        public double Lightness => (Max + Min) / 2;

        public double Hue => Chroma == 0 ? 0 : Degrees(Sextant() * DegreesPerSextant);

        private double Max => Math.Max(Red, Math.Max(Green, Blue));

        private double Min => Math.Min(Red, Math.Min(Green, Blue));

        // Only the first '#' goes, as in the legacy front: "##c8aa6e" stays unreadable.
        public static Rgb? Read(string? hex)
        {
            var digits = hex ?? string.Empty;
            var marker = digits.IndexOf(HexMarker, StringComparison.Ordinal);
            var match = HexTriplet().Match(marker < 0 ? digits : digits.Remove(marker, 1));
            return match.Success
                ? new Rgb(
                    Channel(match, RedGroup),
                    Channel(match, GreenGroup),
                    Channel(match, BlueGroup))
                : null;
        }

        private static double Channel(Match match, int group) =>
            int.Parse(
                match.Groups[group].Value,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture) / ChannelMax;

        private static double Degrees(double degrees) => degrees < 0 ? degrees + FullTurn : degrees;

        // Remainder keeps the dividend's sign, as JavaScript's does: the red sextant may be
        // negative, which Degrees turns back into [0, 360).
        private double Sextant()
        {
            if (Max == Red)
            {
                return ((Green - Blue) / Chroma) % Sextants;
            }

            return Max == Green
                ? ((Blue - Red) / Chroma) + GreenSextant
                : ((Red - Green) / Chroma) + BlueSextant;
        }
    }
}
