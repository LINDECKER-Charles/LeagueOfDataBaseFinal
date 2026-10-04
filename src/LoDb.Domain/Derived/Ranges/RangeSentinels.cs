using System.Globalization;
using System.Text.RegularExpressions;

namespace LoDb.Domain.Derived.Ranges;

/// <summary>
/// Ranges Data Dragon writes as placeholders rather than measures (UP 10).
/// </summary>
/// <remarks>
/// "self", 25000 and beyond, including the unsigned "-1" 4294967295, are not measurable
/// ranges: Aatrox's Q says 25000 and so does Jhin's R. No value beats a made-up one.
/// </remarks>
public static partial class RangeSentinels
{
    /// <summary>Range from which a value means "global" rather than a distance.</summary>
    public const double GlobalRange = 25000;

    private const string SelfRange = "self";
    private const string ZeroRange = "0";
    private const char RankSeparator = '/';

    /// <summary>
    /// The per-rank range of an ability ("600/650/700"), or <see langword="null"/> when it is
    /// missing or a placeholder.
    /// </summary>
    /// <remarks>
    /// The first rank decides, read like the legacy template read it: "0" counts as missing,
    /// and a value with trailing text keeps its leading number. A range with no leading number
    /// at all is masked too.
    /// </remarks>
    public static string? MaskAbilityRange(string? rangeBurn)
    {
        if (string.IsNullOrEmpty(rangeBurn) || rangeBurn is SelfRange or ZeroRange)
        {
            return null;
        }

        var firstRank = rangeBurn.Split(RankSeparator)[0];
        var leadingNumber = LeadingNumber().Match(firstRank);
        return leadingNumber.Success
            && double.Parse(leadingNumber.Value, NumberStyles.Float, CultureInfo.InvariantCulture)
                < GlobalRange
                ? rangeBurn
                : null;
    }

    /// <summary>Whether a range reaches the whole map (Teleport, most ultimates).</summary>
    public static bool IsGlobal(double range) => range >= GlobalRange;

    // A PHP numeric prefix: optional leading whitespace and sign, decimals, exponent.
    [GeneratedRegex(
        @"^[ \t\n\r\v\f]*[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?")]
    private static partial Regex LeadingNumber();
}
