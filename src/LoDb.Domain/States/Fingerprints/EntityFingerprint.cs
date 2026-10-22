using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LoDb.Domain.States.Projections;

namespace LoDb.Domain.States.Fingerprints;

/// <summary>
/// The fingerprint of an en_US entry's displayed projection: two versions of an entity with
/// the same fingerprint are one state, and differ by no field the page shows.
/// </summary>
public static class EntityFingerprint
{
    private const char Separator = ':';

    /// <summary>
    /// Lowercase hexadecimal SHA-256 of a canonical serialization of
    /// <paramref name="projection"/>: fields in the order of <c>ChangeField</c>, subjects
    /// ordinal, values as projected (<see cref="FingerprintScheme.Current"/>).
    /// </summary>
    /// <remarks>
    /// Differs for two projections if and only if <c>EntityDiff.ChangedKeys</c> finds a key:
    /// a state never opens without a field to show.
    /// </remarks>
    public static string Of(IReadOnlyList<ProjectedField> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var serialized = new StringBuilder();
        foreach (var field in DistinctFields.Canonical(projection))
        {
            Write(serialized, field.Key.ToDotted());
            serialized.Append(CultureInfo.InvariantCulture, $"{field.Values.Count}{Separator}");
            foreach (var value in field.Values)
            {
                Write(serialized, value);
            }
        }

        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(serialized.ToString())));
    }

    // Length-prefixed, so no key nor value can pass for a boundary: two projections serialize
    // alike only when they hold the same keys with the same values.
    private static void Write(StringBuilder serialized, string text) =>
        serialized.Append(CultureInfo.InvariantCulture, $"{text.Length}{Separator}{text}");
}
