namespace LoDb.Infrastructure.Persistence.Baseline;

/// <summary>
/// The legacy schema, as the 11 Doctrine migrations leave it, which the <c>Baseline</c>
/// migration reproduces.
/// </summary>
internal static class DoctrineBaseline
{
    /// <summary>Id of the migration, one second after the last Doctrine migration.</summary>
    public const string MigrationId = "20260719150001_Baseline";

    /// <summary>Rows of <c>doctrine_migration_versions</c>, the legacy migration history.</summary>
    public static readonly IReadOnlyList<string> DoctrineVersions =
    [
        @"DoctrineMigrations\Version20260717101320",
        @"DoctrineMigrations\Version20260717131738",
        @"DoctrineMigrations\Version20260717140649",
        @"DoctrineMigrations\Version20260717150000",
        @"DoctrineMigrations\Version20260717180000",
        @"DoctrineMigrations\Version20260717200000",
        @"DoctrineMigrations\Version20260717203909",
        @"DoctrineMigrations\Version20260717214500",
        @"DoctrineMigrations\Version20260719120000",
        @"DoctrineMigrations\Version20260719140000",
        @"DoctrineMigrations\Version20260719150000",
    ];
}
