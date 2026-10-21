using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Cli.Prerender;

/// <summary>
/// What <c>prerender pause</c> and <c>prerender resume</c> share: the active pass, the reason
/// a pause from the command line leaves on it, and the arguments both accept.
/// </summary>
/// <remarks>
/// The reason marks the pass as paused for a deploy, so that <c>prerender resume</c> resumes
/// that pause and never one an operator chose in the admin, unless told so (<c>--any</c>).
/// </remarks>
internal static class ActivePassSwitch
{
    /// <summary>The reason a pause from the command line leaves on its pass.</summary>
    public const string DeployReason =
        "Paused from the command line (prerender pause), for a deploy: prerender resume, or the"
        + " admin, resumes it.";

    /// <summary>Resumes a pass whatever paused it.</summary>
    public const string AnyFlag = "--any";

    // A configuration override the runner passes through: --Section:Key=value.
    private const char ConfigurationSeparator = ':';

    /// <summary>Whether the pre-rendering is configured here; the reason it is not, else.</summary>
    public static string? Unready(IServiceProvider services) =>
        services.GetService<PassControl>() is null
            ? "The pre-rendering is not part of this API's services."
            : services.GetRequiredService<PrerenderActivation>().IsActive
                ? null
                : "Set the page store, PublicHost and SsrBaseUrl (LoDb:Prerender) first.";

    /// <summary>The pass running or paused; null when none is.</summary>
    public static async Task<PrerenderPass?> ActiveAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var contexts = services.GetRequiredService<IDbContextFactory<LoDbDbContext>>();
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.PrerenderPasses.AsNoTracking()
            .SingleOrDefaultAsync(static pass => pass.IsActive, cancellationToken);
    }

    /// <summary>
    /// The arguments past the configuration overrides; null when one is not among
    /// <paramref name="known"/>.
    /// </summary>
    public static IReadOnlySet<string>? Flags(
        IReadOnlyList<string> arguments,
        params string[] known)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var flags = arguments
            .Where(static argument => !IsOverride(argument))
            .ToHashSet(StringComparer.Ordinal);
        return flags.IsSubsetOf(known) ? flags : null;
    }

    private static bool IsOverride(string argument) =>
        argument.Contains(ConfigurationSeparator, StringComparison.Ordinal);
}
