namespace LoDb.Ingestion.Queue;

/// <summary>
/// How on-demand work is admitted: the per-minute budgets of cold work it takes, then how it
/// waits for a slot.
/// </summary>
/// <remarks>
/// A visitor's work takes the budgets and waits a bounded time; the background worker's was
/// admitted when it was queued, and waits as long as it takes. A unit already in flight is
/// joined for free: only the work a call adds is counted, and the datasets of a flight are
/// paid by the flight itself.
/// </remarks>
internal sealed class OnDemandLane
{
    private readonly MinuteBudget? datasets;
    private readonly MinuteBudget? images;
    private readonly Func<Func<CancellationToken, Task>, CancellationToken, Task> run;

    private OnDemandLane(
        MinuteBudget? datasets,
        MinuteBudget? images,
        Func<Func<CancellationToken, Task>, CancellationToken, Task> run)
    {
        this.datasets = datasets;
        this.images = images;
        this.run = run;
    }

    public static OnDemandLane Visitor(OnDemandBudgets budgets, SyncSlots slots)
    {
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(slots);
        return new OnDemandLane(budgets.Datasets, budgets.Images, slots.RunAsync);
    }

    public static OnDemandLane Worker(SyncSlots slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        return new OnDemandLane(null, null, slots.RunQueuedAsync);
    }

    /// <summary>Runs the work in a slot, as the lane waits for one.</summary>
    /// <exception cref="OnDemandRefusedException">A visitor's work found no slot.</exception>
    public Task RunAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken) =>
        run(work, cancellationToken);

    /// <summary>
    /// Takes the budget of one (version, language) to ingest, if the minute has one left;
    /// called by the flight that runs the work, so that its callers pay once.
    /// </summary>
    public bool TakeDataset() => datasets?.TryTake() ?? true;

    /// <summary>
    /// The image units the caller may run: those in flight, then what the budget grants.
    /// </summary>
    public List<string> AdmitImages(SingleFlight flights, IReadOnlyCollection<string> units) =>
        Admit(images, flights, units);

    private static List<string> Admit(
        MinuteBudget? budget,
        SingleFlight flights,
        IReadOnlyCollection<string> units)
    {
        if (budget is null)
        {
            return [.. units];
        }

        var running = flights.InFlight(units);
        List<string> idle = [.. units.Where(unit => !running.Contains(unit))];
        var granted = budget.TakeUpTo(idle.Count);
        return [.. running, .. idle.Take(granted)];
    }
}
