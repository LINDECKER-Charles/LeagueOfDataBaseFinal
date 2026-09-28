namespace LoDb.Infrastructure.Storage;

/// <summary>
/// Write-once publication of a file: readers see either nothing or the complete content.
/// </summary>
/// <remarks>
/// The bytes land in a unique file of the staging area, are flushed to disk, then moved
/// into place without overwriting. The staging area sits inside the root, on the same
/// volume, so the move is a single link or rename. A target that already exists, before or
/// during the move, is left untouched: for content-addressed blobs it holds the same bytes,
/// and a dataset is written once. A failed write leaves at most an orphan in the staging
/// area, which no reader ever looks at.
/// </remarks>
internal sealed class AtomicFileWriter(StorageRoot root)
{
    /// <summary>Publishes the content at the absolute target path.</summary>
    /// <returns>
    /// <c>true</c> if this call wrote the file, <c>false</c> if it already existed.
    /// </returns>
    public async Task<bool> PublishAsync(
        string targetPath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        if (File.Exists(targetPath))
        {
            return false;
        }

        var stagingPath = root.NewStagingPath();
        try
        {
            await StageAsync(stagingPath, content, cancellationToken);
            return MoveIntoPlace(stagingPath, targetPath);
        }
        finally
        {
            DeleteQuietly(stagingPath);
        }
    }

    private static async Task StageAsync(
        string stagingPath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
            BufferSize = 0,
        };
        await using var stream = new FileStream(stagingPath, options);
        await stream.WriteAsync(content, cancellationToken);

        // On disk before the move: a crash never leaves a published file with missing bytes.
        stream.Flush(flushToDisk: true);
    }

    // False when another writer published the same path first: its file stands.
    private static bool MoveIntoPlace(string stagingPath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        return ExclusiveMove.TryPlace(stagingPath, targetPath);
    }

    private static void DeleteQuietly(string stagingPath)
    {
        try
        {
            File.Delete(stagingPath);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: an orphan in the staging area is invisible to every reader.
        }
    }
}
