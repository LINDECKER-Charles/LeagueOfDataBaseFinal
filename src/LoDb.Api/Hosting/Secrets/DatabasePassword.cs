using LoDb.Infrastructure.Persistence;
using Npgsql;

namespace LoDb.Api.Hosting.Secrets;

/// <summary>
/// <c>LoDb:Database:Password</c>: the password of <c>ConnectionStrings:LoDb</c> as a setting
/// of its own, which a served host passes as a secret file and leaves out of the connection
/// string.
/// </summary>
/// <remarks>
/// Npgsql reads a password from a file only in the pgpass format, whose escaping the deploy
/// job would then have to apply. The connection string is completed here instead, once, for
/// every reader of the setting: the data source, the readiness check and the commands such
/// as <c>migrate</c>. Without the setting, the connection string stays as it is configured.
/// </remarks>
internal static class DatabasePassword
{
    public const string Key = "LoDb:Database:Password";

    private const string ConnectionStringsSection = "ConnectionStrings";

    private static readonly string ConnectionStringKey = ConfigurationPath.Combine(
        ConnectionStringsSection,
        LoDbDataSource.ConnectionStringName);

    /// <summary>
    /// Sets the password into the connection string, above every source added before.
    /// </summary>
    public static IConfigurationBuilder AddLoDbDatabasePassword(
        this IConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var password = configuration[Key];
        var connectionString = configuration[ConnectionStringKey];
        if (string.IsNullOrEmpty(password)
            || string.IsNullOrWhiteSpace(connectionString)
            || With(connectionString, password) is not { } completed)
        {
            return configuration;
        }

        return configuration.AddInMemoryCollection([new(ConnectionStringKey, completed)]);
    }

    /// <summary>
    /// <paramref name="connectionString"/> with <paramref name="password"/> as its password;
    /// null when it does not parse.
    /// </summary>
    /// <remarks>
    /// A string that does not parse is left as configured: the data source names the fault
    /// when it is first used, as it would without a password setting.
    /// </remarks>
    public static string? With(string connectionString, string password)
    {
        try
        {
            return new NpgsqlConnectionStringBuilder(connectionString)
            {
                Password = password,
            }.ConnectionString;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
