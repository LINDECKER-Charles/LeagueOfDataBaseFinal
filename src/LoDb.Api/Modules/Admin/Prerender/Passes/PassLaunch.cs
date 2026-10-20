using LoDb.Api.Modules.Admin.Prerender.Pages;

namespace LoDb.Api.Modules.Admin.Prerender.Passes;

/// <summary>What a pass is launched with: the epoch it writes and its perimeter.</summary>
internal sealed record PassLaunch
{
    /// <summary>
    /// The epoch written: the served one to fill it, a new one to replace it (a security fix of
    /// the front, say), switched to once complete. 1 or more.
    /// </summary>
    public required int Epoch { get; init; }

    public PassScope Scope { get; init; } = PassScope.Everything;

    /// <summary>The administrator who launched it; none for the command line.</summary>
    public int? AuthorId { get; init; }
}
