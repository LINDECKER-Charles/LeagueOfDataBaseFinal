namespace LoDb.Api.Modules.Admin.Prerender.Revision.Producing;

/// <summary>What one run of the producer writes into the queue.</summary>
internal sealed record RevisionEnqueue
{
    /// <summary>The pages to render again.</summary>
    public required IReadOnlyList<RevisionPage> Pages { get; init; }

    /// <summary>The epochs they are rendered into: the served one, and the active pass's.</summary>
    public required IReadOnlyList<int> Epochs { get; init; }

    public required DateTimeOffset Now { get; init; }

    /// <summary>
    /// The earliest the drain renders them: once the serving API reads an index that covers
    /// their closure, which the settling delay of a promotion stands for.
    /// </summary>
    public required DateTimeOffset DueAt { get; init; }
}
