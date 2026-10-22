using System.Globalization;

namespace LoDb.Domain.States;

/// <summary>
/// The key of an entity in the index of states: its id, folded with
/// <see cref="string.ToLowerInvariant"/>, for the four resources alike.
/// </summary>
/// <remarks>
/// Data Dragon changed the case of some ids over the years (<c>FiddleSticks</c> up to 3.x,
/// <c>Fiddlesticks</c> since): an ordinal key would read a retirement then a new entity of the
/// same content. Every write and every read of the index folds the same way; the stored path
/// keeps the case of the opener version.
/// </remarks>
public static class EntityKey
{
    /// <param name="id">A champion, item or summoner spell id, as Data Dragon keys it.</param>
    public static string Of(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.ToLowerInvariant();
    }

    /// <param name="id">A rune path id.</param>
    public static string Of(int id) => id.ToString(CultureInfo.InvariantCulture);
}
