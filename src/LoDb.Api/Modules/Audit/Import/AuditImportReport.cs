namespace LoDb.Api.Modules.Audit.Import;

/// <summary>What an import of the legacy journal did.</summary>
internal sealed record AuditImportReport
{
    /// <summary>Day files read: those within the retention.</summary>
    public int Days { get; init; }

    /// <summary>Day files left out, older than the retention.</summary>
    public int ExpiredDays { get; init; }

    /// <summary>Entries added to the journal, or that a dry run would add.</summary>
    public int Imported { get; init; }

    /// <summary>Entries already in the journal, from an earlier import.</summary>
    public int AlreadyPresent { get; init; }

    /// <summary>Lines that are no entry: malformed, without a time, or an unknown action.</summary>
    public int Refused { get; init; }

    public AuditImportReport Add(AuditImportReport day)
    {
        ArgumentNullException.ThrowIfNull(day);
        return new AuditImportReport
        {
            Days = Days + day.Days,
            ExpiredDays = ExpiredDays + day.ExpiredDays,
            Imported = Imported + day.Imported,
            AlreadyPresent = AlreadyPresent + day.AlreadyPresent,
            Refused = Refused + day.Refused,
        };
    }
}
