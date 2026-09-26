using System.Runtime.InteropServices;
using System.Text;

namespace LoDb.Infrastructure.Storage;

/// <summary>
/// Puts a file in place only if nothing is there yet, as one atomic step.
/// </summary>
/// <remarks>
/// On Unix, <c>File.Move(overwrite: false)</c> checks the target then renames: two writers
/// can both pass the check and the second silently replaces the first. <c>link(2)</c>
/// either creates the name or fails with <c>EEXIST</c>, atomically. On Windows the move
/// (<c>MoveFileEx</c> without replace) is already atomic.
/// </remarks>
internal static class ExclusiveMove
{
    // EEXIST has the same value on Linux and macOS.
    private const int FileExistsError = 17;
    private const int Success = 0;
    private const char NulTerminator = '\0';

    /// <summary>
    /// Makes <paramref name="source"/> visible at <paramref name="target"/>. The caller
    /// deletes the source afterwards, whatever the outcome.
    /// </summary>
    /// <returns><c>true</c> if placed, <c>false</c> if the target already existed.</returns>
    public static bool TryPlace(string source, string target)
    {
        if (OperatingSystem.IsWindows())
        {
            return TryMove(source, target);
        }

        if (Link(NativePath(source), NativePath(target)) == Success)
        {
            return true;
        }

        if (Marshal.GetLastPInvokeError() == FileExistsError)
        {
            return false;
        }

        // A file system without hard links: the rename keeps readers safe, only the
        // no-overwrite guarantee weakens to a check.
        return TryMove(source, target);
    }

    private static bool TryMove(string source, string target)
    {
        try
        {
            File.Move(source, target, overwrite: false);
            return true;
        }
        catch (IOException) when (File.Exists(target))
        {
            return false;
        }
    }

    private static byte[] NativePath(string path) =>
        Encoding.UTF8.GetBytes(path + NulTerminator);

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(byte[] existingPath, byte[] newPath);
}
