namespace LoDb.Infrastructure.Jobs;

/// <summary>How one tick of a periodic job ended.</summary>
public enum JobRunOutcome
{
    /// <summary>The job ran and succeeded.</summary>
    Succeeded,

    /// <summary>The job ran and failed, or its lock or schedule could not be reached.</summary>
    Failed,

    /// <summary>Another instance holds the job's lock and is running it.</summary>
    Locked,

    /// <summary>The job already started, here or elsewhere, less than a period ago.</summary>
    NotDue,
}
