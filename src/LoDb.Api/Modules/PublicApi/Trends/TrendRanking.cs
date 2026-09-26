using System.ComponentModel;

namespace LoDb.Api.Modules.PublicApi.Trends;

/// <summary>A computed ranking, as the cache holds it.</summary>
/// <remarks>
/// Sealed and marked immutable, so the cache hands out this instance instead of a copy.
/// </remarks>
[ImmutableObject(true)]
internal sealed record TrendRanking(IReadOnlyList<TrendEntry> Entries);
