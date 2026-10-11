using System.Reflection;
using Npgsql;

namespace LoDb.Infrastructure.Persistence.Roles;

/// <summary>
/// The owners and grants of the schema, <c>schema-privileges.sql</c>: run whole, in the
/// transaction of <see cref="DatabaseRoles"/>, once the roles exist.
/// </summary>
internal static class SchemaPrivileges
{
    private const string Resource = "schema-privileges.sql";

    private static readonly Lazy<string> Script = new(ReadScript);

    public static async Task ApplyAsync(
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            Script.Value,
            transaction.Connection,
            transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ReadScript()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException($"Missing embedded resource {Resource}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
