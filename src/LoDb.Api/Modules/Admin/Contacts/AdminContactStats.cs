namespace LoDb.Api.Modules.Admin.Contacts;

/// <summary>The counters above the message list, over every message.</summary>
internal sealed record AdminContactStats
{
    public required int Total { get; init; }

    public required int New { get; init; }

    public required int Handled { get; init; }

    /// <summary>Messages received over the last seven days.</summary>
    public required int Week { get; init; }
}
