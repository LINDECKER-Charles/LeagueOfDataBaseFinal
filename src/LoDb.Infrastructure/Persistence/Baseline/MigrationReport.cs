namespace LoDb.Infrastructure.Persistence.Baseline;

/// <summary>What <c>migrate</c> or <c>baseline mark-applied</c> did.</summary>
/// <param name="Baseline">State of the database with regard to the baseline.</param>
/// <param name="Applied">Migrations applied by this run, in order.</param>
/// <param name="Differences">
/// For an unexpected schema, the catalog lines the database lacks (<c>-</c>) or has in
/// excess (<c>+</c>) compared with the Doctrine schema.
/// </param>
public sealed record MigrationReport(
    BaselineOutcome Baseline,
    IReadOnlyList<string> Applied,
    IReadOnlyList<string> Differences);
