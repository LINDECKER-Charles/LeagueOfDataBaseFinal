namespace LoDb.Infrastructure.Persistence.Roles;

/// <summary>
/// The roles <c>migrate</c> keeps in place on the database server, besides the superuser it
/// connects as (<see cref="DatabaseRoles"/>, <c>schema-privileges.sql</c>).
/// </summary>
public static class DatabaseRoleNames
{
    /// <summary>Owns the objects of the schema. Nobody logs in as it.</summary>
    public const string Owner = "lodb_owner";

    /// <summary>
    /// Owns <c>analytics_event</c>, its daily partitions and the two functions that create
    /// and drop them. Nobody logs in as it.
    /// </summary>
    public const string Partitions = "lodb_partitions";

    /// <summary>The API's role: reads and writes rows, runs the two partition functions.</summary>
    public const string App = "lodb_app";
}
