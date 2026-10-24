namespace LoDb.Api.Modules.Analytics.Capture;

/// <summary>
/// The body of a post of nginx to the robots journal, read whole into memory up to
/// <see cref="MaxBytes"/>.
/// </summary>
internal static class JournalBody
{
    /// <summary>
    /// Far above a batch of njs (500 records of under a kilobyte each), below what a stuck
    /// sender could make the API hold.
    /// </summary>
    public const int MaxBytes = 4 * 1024 * 1024;

    private const int ChunkBytes = 64 * 1024;

    /// <returns>The body; null when it is longer than <see cref="MaxBytes"/>.</returns>
    public static async Task<ReadOnlyMemory<byte>?> ReadAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContentLength > MaxBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[ChunkBytes];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }
}
