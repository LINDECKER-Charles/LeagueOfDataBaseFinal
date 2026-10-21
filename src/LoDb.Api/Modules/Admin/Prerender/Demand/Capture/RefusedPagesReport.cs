namespace LoDb.Api.Modules.Admin.Prerender.Demand.Capture;

/// <summary>
/// What the visitors' SSR answers to <c>GET /refused?after={cursor}</c> on its metrics port
/// (src/LoDb.Web/src/server/demand): the pages whose count grew after the cursor.
/// </summary>
internal sealed record RefusedPagesReport
{
    /// <summary>Drawn at the SSR's start: another one means its counts started again.</summary>
    public required string Instance { get; init; }

    /// <summary>Where the next read starts.</summary>
    public required long Cursor { get; init; }

    /// <summary>Whether every change after the cursor asked for is in this answer.</summary>
    public required bool IsComplete { get; init; }

    /// <summary>Paths the SSR dropped since its start to bound its memory.</summary>
    public long Evicted { get; init; }

    public required IReadOnlyList<RefusedPageCount> Pages { get; init; }
}
