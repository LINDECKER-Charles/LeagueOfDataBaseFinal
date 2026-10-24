namespace LoDb.Infrastructure.Robots.Reports.Reading;

/// <summary>The days of a report, both included, in UTC.</summary>
internal readonly record struct DatePeriod(DateOnly From, DateOnly To)
{
    public int Days => To.DayNumber - From.DayNumber + 1;

    /// <summary>Every day of the period, in order.</summary>
    public IReadOnlyList<DateOnly> Dates =>
        [.. Enumerable.Range(0, Days).Select(From.AddDays)];
}
