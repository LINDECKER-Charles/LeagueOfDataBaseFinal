using System.Text.Json;
using System.Text.Json.Nodes;
using LoDb.Desktop.Lifecycle;

namespace LoDb.Desktop.Bridge.Commands;

/// <summary>
/// <c>updateState {}</c> → <c>{state, version}</c>, where <c>state</c> is the front's
/// <c>UpdateState</c>: <c>none</c>, <c>downloading</c> or <c>ready</c>.
/// </summary>
internal sealed class UpdateStateCommand(IDesktopUpdates updates) : IBridgeCommand
{
    public const string Name = "updateState";

    private const string StateMember = "state";
    private const string VersionMember = "version";

    public string Type => Name;

    public Task<BridgeOutcome> ExecuteAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var current = updates.Current;
        return Task.FromResult(BridgeOutcome.Success(new JsonObject
        {
            [StateMember] = JsonNamingPolicy.CamelCase.ConvertName(current.Stage.ToString()),
            [VersionMember] = current.Version,
        }));
    }
}
