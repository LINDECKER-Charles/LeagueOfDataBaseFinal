using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Writing;

namespace LoDb.Api.Modules.Admin.Prerender.Units;

/// <summary>
/// The pages of a unit in flight together, <see cref="PrerenderOptions.MaxInFlight"/> at most,
/// and the cursor they leave behind them: the first page that did not end.
/// </summary>
/// <remarks>
/// Pages end out of order. One is saved only once every page before it ended, with them: a
/// restart renders again the few that ended past a page still in flight, and counts none
/// twice.
/// </remarks>
internal sealed class PageWindow(int cursor, int count, int width)
{
    private readonly Dictionary<Task<PageResult>, int> _inFlight = [];
    private readonly SortedDictionary<int, PageResult> _ended = [];
    private int _cursor = cursor;
    private int _next = cursor;

    public bool IsIdle => _inFlight.Count == 0;

    /// <summary>Starts the next pages, until the window is full or no page is left.</summary>
    public void Fill(Func<int, Task<PageResult>> start)
    {
        ArgumentNullException.ThrowIfNull(start);
        for (; _inFlight.Count < width && _next < count; _next++)
        {
            _inFlight.Add(start(_next), _next);
        }
    }

    /// <summary>Waits for a page in flight to end: its index, and how it ended.</summary>
    public async Task<(int Index, PageResult Result)> NextEndedAsync()
    {
        var ended = await Task.WhenAny(_inFlight.Keys);
        var index = _inFlight[ended];
        _inFlight.Remove(ended);
        return (index, await ended);
    }

    /// <summary>
    /// Notes that the page at <paramref name="index"/> ended; the pages now ended in a row from
    /// the cursor, to save, or null when one before it is still in flight.
    /// </summary>
    public UnitStep? End(int index, PageResult result)
    {
        _ended.Add(index, result);
        var step = new UnitStep { Cursor = _cursor };
        while (_ended.Remove(step.Cursor, out var page))
        {
            step = step.Then(page);
        }

        if (step.Cursor == _cursor)
        {
            return null;
        }

        _cursor = step.Cursor;
        return step;
    }

    /// <summary>
    /// Waits for the pages still in flight, however they end: none keeps writing behind a
    /// unit that stopped on an error.
    /// </summary>
    public async Task SettleAsync() =>
        await Task.WhenAll(_inFlight.Keys.Cast<Task>())
            .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
}
