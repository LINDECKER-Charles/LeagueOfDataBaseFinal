using System.Globalization;

namespace LoDb.Api.Modules.Accounts.Links;

/// <summary>
/// What a throttle row of <see cref="MailThrottle"/> holds: how many e-mails the account ever
/// asked for, then when it asked for those still within the window, in Unix milliseconds,
/// oldest first, such as <c>6;1760000000000 1760086400000</c>.
/// </summary>
/// <remarks>
/// A row written before the total was kept holds the times alone: its total is their number,
/// those of its last quarter of an hour, which may give such an account a few e-mails more.
/// </remarks>
/// <param name="Total">E-mails asked for over the life of the account.</param>
/// <param name="Recent">Times of the e-mails within the window, oldest first.</param>
internal sealed record MailTally(int Total, IReadOnlyList<DateTimeOffset> Recent)
{
    private const char TotalSeparator = ';';
    private const char TimeSeparator = ' ';

    /// <summary>
    /// The tally of <paramref name="value"/>, its times after <paramref name="since"/>.
    /// </summary>
    public static MailTally Parse(string? value, DateTimeOffset since)
    {
        var text = value ?? string.Empty;
        var split = text.IndexOf(TotalSeparator, StringComparison.Ordinal);
        var times = Times(split < 0 ? text : text[(split + 1)..]);
        var total = split >= 0 && TryParseCount(text[..split], out var stored)
            ? Math.Max(stored, times.Count)
            : times.Count;
        return new MailTally(total, [.. times.Where(sent => sent > since).Order()]);
    }

    /// <summary>The tally once one more e-mail is asked for at <paramref name="sent"/>.</summary>
    public MailTally Add(DateTimeOffset sent) => new(Total + 1, [.. Recent, sent]);

    /// <summary>The text of the row.</summary>
    public string Format() =>
        Total.ToString(CultureInfo.InvariantCulture)
        + TotalSeparator
        + string.Join(
            TimeSeparator,
            Recent.Select(static sent =>
                sent.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)));

    private static List<DateTimeOffset> Times(string text) =>
    [
        .. text.Split(TimeSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => long.TryParse(
                part,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var milliseconds)
                ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
                : DateTimeOffset.MinValue),
    ];

    private static bool TryParseCount(string text, out int count) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out count);
}
