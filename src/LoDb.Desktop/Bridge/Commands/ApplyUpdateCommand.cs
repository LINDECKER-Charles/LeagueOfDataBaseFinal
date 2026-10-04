using System.Text.Json;
using System.Text.Json.Nodes;
using LoDb.Desktop.Lifecycle;

namespace LoDb.Desktop.Bridge.Commands;

/// <summary>
/// <c>applyUpdate {}</c> → <c>{applying: true}</c>, then the app restarts on the new
/// version; <c>no-update</c> when none is ready.
/// </summary>
internal sealed class ApplyUpdateCommand(IDesktopUpdates updates) : IBridgeCommand
{
    public const string Name = "applyUpdate";

    private const string ApplyingMember = "applying";

    public string Type => Name;

    public async Task<BridgeOutcome> ExecuteAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (updates.Current.Stage != UpdateStage.Ready
            || !await updates.ApplyAsync(cancellationToken))
        {
            return BridgeOutcome.Failure(BridgeErrors.NoUpdate);
        }

        return BridgeOutcome.Success(new JsonObject { [ApplyingMember] = true });
    }
}
