namespace LoDb.Infrastructure.Robots.Retention;

/// <summary>The daily upkeep of the robots journal.</summary>
public interface IRobotRetention
{
    /// <summary>Deletes the hits older than <see cref="RobotsOptions.HitRetention"/>.</summary>
    /// <returns>The hits deleted.</returns>
    Task<int> ApplyAsync(CancellationToken cancellationToken);
}
