using System.Security.Cryptography;
using System.Text;
using LoDb.Desktop.Hosting;
using Microsoft.AspNetCore.DataProtection;

namespace LoDb.Desktop.Auth.Tokens;

/// <summary>
/// The refresh token of a "remember me" sign-in, encrypted at rest by the local Data
/// Protection keys (ADR 0009). Anything unreadable reads as "no token": the user signs in
/// again, which is safer than failing the start of the app.
/// </summary>
internal sealed partial class RefreshTokenVault(
    IDataProtectionProvider protection,
    DesktopOptions options,
    ILogger<RefreshTokenVault> logger)
{
    /// <summary>
    /// Purpose of the protector: a key ring shared with another use could not decrypt it,
    /// and "v1" lets a future format move on without reading this one wrongly.
    /// </summary>
    public const string Purpose = "LoDb.Desktop.RefreshToken.v1";

    public const string FileName = "refresh-token.bin";

    private const string PendingSuffix = ".pending";

    private readonly IDataProtector _protector = protection.CreateProtector(Purpose);

    public string FilePath =>
        Path.Combine(options.DataDirectory, DataDirectory.SessionFolder, FileName);

    public string? Load()
    {
        byte[] sealedToken;
        try
        {
            sealedToken = File.ReadAllBytes(FilePath);
        }
        catch (Exception exception) when (exception is FileNotFoundException
            or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            LogUnreadable(logger, exception);
            return null;
        }

        return Unprotect(sealedToken);
    }

    /// <summary>Writes the token atomically: a crash never leaves half a file behind.</summary>
    public void Save(string refreshToken)
    {
        var pending = FilePath + PendingSuffix;
        try
        {
            DataDirectory.CreatePrivate(Path.GetDirectoryName(FilePath)!);
            File.WriteAllBytes(pending, _protector.Protect(Encoding.UTF8.GetBytes(refreshToken)));
            File.Move(pending, FilePath, overwrite: true);
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            // The session still holds the token in memory; only the restart forgets it.
            LogUnwritable(logger, exception);
        }
    }

    public void Delete()
    {
        try
        {
            File.Delete(FilePath);
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            LogUnwritable(logger, exception);
        }
    }

    private string? Unprotect(byte[] sealedToken)
    {
        try
        {
            return Encoding.UTF8.GetString(_protector.Unprotect(sealedToken));
        }
        catch (CryptographicException exception)
        {
            // Keys lost or rotated out, or a file altered: the token is gone.
            LogUnreadable(logger, exception);
            return null;
        }
    }

    private static bool IsFileSystemFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;

    [LoggerMessage(
        EventName = "desktop.vault.unreadable",
        Level = LogLevel.Warning,
        Message = "The saved refresh token could not be read; the user signs in again.")]
    private static partial void LogUnreadable(ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = "desktop.vault.unwritable",
        Level = LogLevel.Warning,
        Message = "The saved refresh token could not be written or deleted.")]
    private static partial void LogUnwritable(ILogger logger, Exception exception);
}
