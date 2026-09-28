using System.Text.Json;
using System.Text.Json.Nodes;
using LoDb.Desktop.Bridge.Validation;
using LoDb.Desktop.Shell;

namespace LoDb.Desktop.Bridge.Commands;

/// <summary>
/// <c>saveFile {name, mime, base64}</c> → <c>{saved}</c>: the native save dialog, then the
/// file written where the user chose. The path is never returned to the page.
/// </summary>
internal sealed partial class SaveFileCommand(
    IDesktopShell shell,
    ILogger<SaveFileCommand> logger) : IBridgeCommand
{
    public const string Name = "saveFile";

    private const string NameField = "name";
    private const string MimeField = "mime";
    private const string ContentField = "base64";
    private const string SavedMember = "saved";

    private int _isSaving;

    public string Type => Name;

    public async Task<BridgeOutcome> ExecuteAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var name = BridgePayload.StringOf(payload, NameField);
        if (!SaveFileName.IsValid(name))
        {
            return BridgeOutcome.Failure(BridgeErrors.InvalidName);
        }

        if (!MimeType.IsValid(BridgePayload.StringOf(payload, MimeField)))
        {
            return BridgeOutcome.Failure(BridgeErrors.InvalidMime);
        }

        var error = FileContent.TryDecode(
            BridgePayload.StringOf(payload, ContentField),
            out var content);
        if (error is not null)
        {
            return BridgeOutcome.Failure(error);
        }

        // One dialog at a time: a second one would queue behind a modal the user is using.
        if (Interlocked.Exchange(ref _isSaving, 1) == 1)
        {
            return BridgeOutcome.Failure(BridgeErrors.Busy);
        }

        try
        {
            return await SaveAsync(name, content, cancellationToken);
        }
        finally
        {
            Volatile.Write(ref _isSaving, 0);
        }
    }

    private async Task<BridgeOutcome> SaveAsync(
        string name,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var path = await shell.PickSaveFileAsync(name, cancellationToken);
        if (path is null)
        {
            return Saved(false);
        }

        try
        {
            await File.WriteAllBytesAsync(path, content, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogWriteFailed(logger, exception);
            return BridgeOutcome.Failure(BridgeErrors.WriteFailed);
        }

        return Saved(true);
    }

    private static BridgeOutcome Saved(bool isSaved) =>
        BridgeOutcome.Success(new JsonObject { [SavedMember] = isSaved });

    [LoggerMessage(
        EventName = "desktop.file.write_failed",
        Level = LogLevel.Warning,
        Message = "A file chosen in the save dialog could not be written.")]
    private static partial void LogWriteFailed(ILogger logger, Exception exception);
}
