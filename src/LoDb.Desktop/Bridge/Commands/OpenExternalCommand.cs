using System.Text.Json;
using LoDb.Desktop.Bridge.Validation;
using LoDb.Desktop.Shell;

namespace LoDb.Desktop.Bridge.Commands;

/// <summary>
/// <c>openExternal {url}</c>: opens a remote http(s) page in the system browser, as
/// <c>target="_blank"</c> does nothing in the WebView (spike report).
/// </summary>
internal sealed class OpenExternalCommand(ISystemBrowser browser) : IBridgeCommand
{
    public const string Name = "openExternal";

    private const string UrlField = "url";

    public string Type => Name;

    public Task<BridgeOutcome> ExecuteAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var outcome = !ExternalUrl.TryParse(BridgePayload.StringOf(payload, UrlField), out var url)
            ? BridgeOutcome.Failure(BridgeErrors.InvalidUrl)
            : browser.Open(url)
                ? BridgeOutcome.Success()
                : BridgeOutcome.Failure(BridgeErrors.BrowserFailed);
        return Task.FromResult(outcome);
    }
}
