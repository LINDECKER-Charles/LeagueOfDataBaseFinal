using LoDb.Api.Modules.ClientPolicy;
using LoDb.Api.Modules.ClientPolicy.Policy;
using LoDb.Api.Modules.ClientPolicy.Publishing;

namespace LoDb.Api.Cli.ClientPolicy;

/// <summary>
/// <c>client-policy publish</c>: publishes the policy of one app, as the admin endpoint
/// does, from a release workflow or an operator's shell (ADR 0008).
/// </summary>
/// <remarks>
/// <para>
/// The options state the whole policy of the app, which replaces the one published before:
/// an option left out clears its value. A bundle is Android's only, and takes all five
/// <c>--bundle-*</c> options. See <see cref="ClientPolicyPublishArguments.Usage"/>.
/// </para>
/// <para>
/// Exit code 0 once published, with the policy on the standard output; 1 when the database
/// fails; 2 on invalid arguments, each invalid field on the standard error.
/// </para>
/// </remarks>
internal sealed class ClientPolicyPublishCommand(IServiceProvider services) : ICliCommand
{
    public static string Name => "client-policy publish";

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ClientPolicyPublishArguments options;
        try
        {
            options = ClientPolicyPublishArguments.Parse(arguments);
        }
        catch (FormatException exception)
        {
            await Console.Error.WriteLineAsync(
                $"{exception.Message} {ClientPolicyPublishArguments.Usage}");
            return CliExitCodes.Usage;
        }

        var refusals = PublishPolicyRules.Check(options.Platform, options.Request).ToProblem();
        if (refusals.Errors is { Count: > 0 } errors)
        {
            foreach (var (field, codes) in errors)
            {
                await Console.Error.WriteLineAsync($"{field}: {string.Join(", ", codes)}");
            }

            return CliExitCodes.Usage;
        }

        // Resolved here, as in migrate: a missing setting fails as one logged line.
        var store = services.GetRequiredService<ClientPolicyStore>();
        var published = await store.PublishAsync(
            options.Platform,
            options.Request,
            cancellationToken);
        await Console.Out.WriteLineAsync(Summary(published));
        return CliExitCodes.Success;
    }

    private static string Summary(PlatformPolicy policy) =>
        $"Published the {ClientPlatforms.NameOf(policy.Platform)} policy: minimum"
        + $" {policy.MinimumVersion ?? "none"}, latest {policy.LatestVersion ?? "none"},"
        + $" bundle {policy.Bundle?.Id ?? "none"}.";
}
