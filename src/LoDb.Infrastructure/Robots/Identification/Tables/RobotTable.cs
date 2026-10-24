namespace LoDb.Infrastructure.Robots.Identification.Tables;

/// <summary>Turns the compact rows of a category into profiles.</summary>
internal static class RobotTable
{
    private const char TokenSeparator = '|';

    public static IReadOnlyList<RobotProfile> Of(string category, IReadOnlyList<RobotRow> rows) =>
    [
        .. rows.Select(row => new RobotProfile
        {
            Key = row.Key,
            Name = row.Name,
            Operator = row.Operator,
            Category = category,
            Tokens = row.Tokens.Split(TokenSeparator),
            VerifyDomains = row.Domains ?? [],
        }),
    ];
}
