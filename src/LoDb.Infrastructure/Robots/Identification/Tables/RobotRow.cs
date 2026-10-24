namespace LoDb.Infrastructure.Robots.Identification.Tables;

/// <summary>
/// A row of a table of the catalog: key, name, operator, the tokens of its user agent split
/// by <c>|</c>, and its verification domains, if any.
/// </summary>
internal readonly record struct RobotRow(
    string Key,
    string Name,
    string Operator,
    string Tokens,
    IReadOnlyList<string>? Domains = null);
