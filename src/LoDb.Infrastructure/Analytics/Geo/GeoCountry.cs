namespace LoDb.Infrastructure.Analytics.Geo;

/// <summary>The country of an address.</summary>
/// <param name="Code">ISO 3166 alpha-2 code, such as <c>FR</c>.</param>
/// <param name="Name">English name, or the code when the database has none.</param>
internal sealed record GeoCountry(string Code, string Name);
