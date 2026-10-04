namespace LoDb.Domain.Builds.Votes;

/// <summary>
/// One vote per voter and build, toggled: a vote is cast, switched by the other direction and
/// withdrawn by the same one again. Only the net score is ever shown.
/// </summary>
public static class VoteRules
{
    /// <summary>The value of no vote, as <c>myVote</c> reports it.</summary>
    public const int None = 0;

    public const string UpCode = "up";
    public const string DownCode = "down";

    /// <summary>Reads <c>up</c> or <c>down</c>, exactly as the API writes them.</summary>
    public static bool TryParse(string? code, out VoteDirection direction)
    {
        direction = code switch
        {
            UpCode => VoteDirection.Up,
            DownCode => VoteDirection.Down,
            _ => default,
        };
        return direction != default;
    }

    /// <summary>
    /// The voter's value once <paramref name="cast"/> applies to <paramref name="current"/>,
    /// their standing value or <see cref="None"/>: <see cref="None"/> when it withdraws it.
    /// </summary>
    public static int Toggle(int current, VoteDirection cast) =>
        current == (int)cast ? None : (int)cast;
}
