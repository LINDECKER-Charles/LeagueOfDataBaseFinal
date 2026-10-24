using Npgsql;
using NpgsqlTypes;

namespace LoDb.Infrastructure.Robots.Reports.Reading;

/// <summary>
/// The SQL of a <see cref="RobotSelection"/>: its condition, for a table holding
/// <c>detection</c>, <c>agent_id</c> and <c>network</c>, and its parameters, null for the
/// filters it lacks.
/// </summary>
internal static class SelectionFilter
{
    /// <summary>
    /// The condition; <c>robot_hit</c> keeps a missing network as null, <c>robot_daily</c> as
    /// an empty text.
    /// </summary>
    public const string Sql = """
        (@detection IS NULL OR detection = @detection)
        AND (@agents IS NULL OR agent_id = ANY(@agents))
        AND (@network IS NULL OR coalesce(network, '') = @network)
        """;

    public static void AddTo(NpgsqlCommand command, RobotSelection? selection)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Parameters.Add(Text("detection", selection?.Detection));
        var agents = new NpgsqlParameter("agents", NpgsqlDbType.Array | NpgsqlDbType.Integer)
        {
            Value = selection?.AgentIds is { } ids ? ids.ToArray() : (object)DBNull.Value,
        };
        command.Parameters.Add(agents);
        command.Parameters.Add(Text("network", selection?.Network));
    }

    /// <summary>A text parameter, typed even when null.</summary>
    public static NpgsqlParameter Text(string name, string? value) =>
        new(name, NpgsqlDbType.Text) { Value = value is null ? DBNull.Value : (object)value };
}
