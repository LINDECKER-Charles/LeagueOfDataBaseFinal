using System.Text.Json;

namespace LoDb.Desktop.Bridge;

/// <summary>
/// A message type of the bridge. The payload comes from the page, which the host does not
/// trust: every field is validated before use.
/// </summary>
internal interface IBridgeCommand
{
    /// <summary>The <c>type</c> of the messages it answers, such as <c>openExternal</c>.</summary>
    string Type { get; }

    /// <param name="payload">The <c>payload</c> of the message, always an object.</param>
    /// <param name="cancellationToken">Cancelled when the app stops.</param>
    Task<BridgeOutcome> ExecuteAsync(JsonElement payload, CancellationToken cancellationToken);
}
