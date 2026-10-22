using System.Text.Json;
using LoDb.Domain.Derived.Stats;

namespace LoDb.Domain.States.Projections.Parts;

/// <summary>
/// A stat as a field subject: its <see cref="GameStat"/> in snake_case, the key of its
/// <c>stat.*</c> label and the API's own spelling.
/// </summary>
internal static class StatTokens
{
    public static string Of(GameStat stat) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(stat.ToString());
}
