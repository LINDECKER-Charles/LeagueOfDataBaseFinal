namespace LoDb.Infrastructure.Persistence.Baseline;

/// <summary>Where the database stood with regard to the <c>Baseline</c> migration.</summary>
public enum BaselineOutcome
{
    /// <summary>Already in the EF history: nothing to do.</summary>
    AlreadyApplied,

    /// <summary>The database held exactly the Doctrine schema: Baseline is now recorded.</summary>
    Marked,

    /// <summary>No table at all: <c>migrate</c> creates everything.</summary>
    EmptyDatabase,

    /// <summary>Neither empty nor the Doctrine schema: nothing was changed.</summary>
    UnexpectedSchema,
}
