namespace LoDb.Api.Modules.Admin.Monitoring.Views;

/// <summary>A dependency of the API, checked by its readiness probe.</summary>
internal sealed record ServiceProbe
{
    /// <summary>postgres or storage, the names of the readiness checks.</summary>
    public required string Name { get; init; }

    /// <summary>ok, degraded or down.</summary>
    public required string Status { get; init; }

    public required long LatencyMs { get; init; }

    /// <summary>Why it is not ok, shortened; null when it is.</summary>
    public string? Detail { get; init; }

    /// <summary>The server version, for PostgreSQL.</summary>
    public string? Version { get; init; }

    /// <summary>Size of the database, for PostgreSQL.</summary>
    public long? DatabaseBytes { get; init; }

    /// <summary>
    /// Objects of the storage root, for the storage, when its report is kept from an earlier
    /// walk: the probe never walks the root itself.
    /// </summary>
    public long? Objects { get; init; }

    /// <summary>Bytes of the storage root, known when <c>objects</c> is.</summary>
    public long? Bytes { get; init; }
}
