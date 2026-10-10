using LoDb.Infrastructure.Persistence.Outbox;

namespace LoDb.Infrastructure.Outbox.Delivery;

/// <summary>
/// What one batch may take of each template that has messages due: what is left of its
/// hourly cap, a batch at most; and the due messages the caps hold back meanwhile.
/// </summary>
/// <remarks>
/// A stored template the code does not know has no cap: its messages are taken, and die on
/// their unreadable template, as before the caps.
/// </remarks>
internal sealed record SendingBudget
{
    private static readonly Dictionary<string, EmailTemplate> Known =
        Enum.GetValues<EmailTemplate>().ToDictionary(OutboxColumns.ToText, StringComparer.Ordinal);

    /// <summary>The templates the batch may take, each with its share below.</summary>
    public required IReadOnlyList<string> Templates { get; init; }

    /// <summary>How many messages of each of <see cref="Templates"/> the batch may take.</summary>
    public required IReadOnlyList<int> Allowances { get; init; }

    /// <summary>Templates whose cap is reached while some of their messages are due.</summary>
    public required IReadOnlyList<string> Capped { get; init; }

    /// <summary>Due messages deferred by the caps, not counting the batch size.</summary>
    public required long HeldBack { get; init; }

    /// <summary>Whether the batch may take nothing.</summary>
    public bool IsEmpty => Templates.Count == 0;

    public static SendingBudget Of(IReadOnlyList<TemplateLoad> loads, OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(loads);
        ArgumentNullException.ThrowIfNull(options);
        var open = loads
            .Select(load => (load, Left: Left(load, options.HourlyCaps)))
            .ToList();
        return new SendingBudget
        {
            Templates = [.. open.Where(static pair => pair.Left > 0)
                .Select(static pair => pair.load.Template)],
            Allowances = [.. open.Where(static pair => pair.Left > 0)
                .Select(pair => (int)Math.Min(pair.Left, options.BatchSize))],
            Capped = [.. open.Where(static pair => pair.Left == 0)
                .Select(static pair => pair.load.Template)],
            HeldBack = open.Sum(static pair => Math.Max(0, pair.load.Due - pair.Left)),
        };
    }

    // What the hour leaves to the template; a batch's worth for one without a cap.
    private static long Left(TemplateLoad load, OutboxHourlyCaps caps) =>
        Known.TryGetValue(load.Template, out var template)
            ? Math.Max(0, caps.Of(template) - load.Sent)
            : long.MaxValue;
}
