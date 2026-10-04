using System.Collections.Frozen;
using System.Text.Json;

namespace LoDb.Desktop.Bridge;

/// <summary>
/// The bridge between the page and the host (plan §5.2): a JSON message
/// <c>{id, type, payload}</c> in, a reply <c>{id, ok, result | error}</c> out. It is
/// limited to what the WebView cannot do itself (ADR 0007).
/// </summary>
internal sealed partial class DesktopBridge
{
    /// <summary>
    /// Longest message accepted: the largest file <c>saveFile</c> carries, in base64, and
    /// room for the rest of the message.
    /// </summary>
    public const int MaxMessageLength = Validation.FileContent.MaxEncodedLength + 64 * 1024;

    private const string TooLongReason = "too-long";
    private const string UnreadableReason = "unreadable";

    private readonly FrozenDictionary<string, IBridgeCommand> _commands;
    private readonly ILogger<DesktopBridge> _logger;

    public DesktopBridge(IEnumerable<IBridgeCommand> commands, ILogger<DesktopBridge> logger)
    {
        _commands = commands.ToFrozenDictionary(command => command.Type, StringComparer.Ordinal);
        _logger = logger;
    }

    /// <returns>The reply to post back; null when the message is dropped.</returns>
    public async Task<string?> HandleAsync(string message, CancellationToken cancellationToken)
    {
        if (message.Length > MaxMessageLength)
        {
            LogDropped(_logger, TooLongReason);
            return null;
        }

        using var document = Parse(message);
        if (document is null || BridgeRequest.From(document.RootElement) is not { } request)
        {
            LogDropped(_logger, UnreadableReason);
            return null;
        }

        var outcome = await DispatchAsync(request, cancellationToken);
        return outcome.ToReply(request.Id);
    }

    private async Task<BridgeOutcome> DispatchAsync(
        BridgeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Type is null || request.Payload.ValueKind != JsonValueKind.Object)
        {
            return BridgeOutcome.Failure(BridgeErrors.InvalidMessage);
        }

        if (!_commands.TryGetValue(request.Type, out var command))
        {
            return BridgeOutcome.Failure(BridgeErrors.UnknownType);
        }

        try
        {
            return await command.ExecuteAsync(request.Payload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCommandFailed(_logger, request.Type, exception);
            return BridgeOutcome.Failure(BridgeErrors.Internal);
        }
    }

    private static JsonDocument? Parse(string message)
    {
        try
        {
            return JsonDocument.Parse(message);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(
        EventName = "desktop.bridge.dropped",
        Level = LogLevel.Warning,
        Message = "A bridge message was dropped: {Reason}.")]
    private static partial void LogDropped(ILogger logger, string reason);

    [LoggerMessage(
        EventName = "desktop.bridge.failed",
        Level = LogLevel.Error,
        Message = "The bridge command {Type} failed.")]
    private static partial void LogCommandFailed(ILogger logger, string type, Exception exception);
}
