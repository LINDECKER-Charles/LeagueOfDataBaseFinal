namespace LoDb.Infrastructure.Analytics.Import;

/// <summary>What an import of the legacy daily files did, or would do on a dry run.</summary>
public sealed record DailyImportReport
{
    /// <summary>Day files read.</summary>
    public required int Files { get; init; }

    /// <summary>Days added to <c>analytics_daily</c>.</summary>
    public required int Imported { get; init; }

    /// <summary>Days left alone: they already have an aggregate.</summary>
    public required int AlreadyPresent { get; init; }

    /// <summary>Files refused: a name that is no day, or a content that is no aggregate.</summary>
    public required int Refused { get; init; }
}
