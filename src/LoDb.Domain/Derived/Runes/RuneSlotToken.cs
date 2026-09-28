using System.Globalization;

namespace LoDb.Domain.Derived.Runes;

/// <summary>
/// Language-independent token of a rune row: "keystone" for the first, "row1" to "row3" for
/// the minor rows.
/// </summary>
/// <remarks>
/// A rune carries no attribute worth filtering on: its row is positional, so the token is
/// derived from the slot index in its tree.
/// </remarks>
public static class RuneSlotToken
{
    public const string Keystone = "keystone";

    private const int KeystoneSlot = 0;
    private const string RowPrefix = "row";

    public static string Of(int slotIndex) =>
        slotIndex == KeystoneSlot
            ? Keystone
            : string.Create(CultureInfo.InvariantCulture, $"{RowPrefix}{slotIndex}");
}
