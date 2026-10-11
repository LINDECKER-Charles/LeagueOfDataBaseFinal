using LoDb.Api.Modules.Accounts.Security.Limits;

namespace LoDb.Api.Modules.Builds.Http;

/// <summary>
/// The writes an account may make on the builds: saving one checks it against a catalog,
/// and a vote moves the public rankings.
/// </summary>
internal static class BuildLimits
{
    private const int EditsPerHour = 60;
    private const int VotesPerHour = 120;

    /// <summary>Builds created or replaced, 60 an hour per account by default.</summary>
    public static AccountActionLimit Edit { get; } =
        new(Policy: "build-edit", Setting: "BuildEditsPerHour", EditsPerHour);

    /// <summary>Votes cast, switched or withdrawn, 120 an hour per account by default.</summary>
    public static AccountActionLimit Vote { get; } =
        new(Policy: "build-vote", Setting: "VotesPerHour", VotesPerHour);
}
