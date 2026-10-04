namespace LoDb.Desktop.Bridge.Validation;

/// <summary>The <c>base64</c> content of <c>saveFile</c>, bounded before it is decoded.</summary>
internal static class FileContent
{
    /// <summary>
    /// Largest file the bridge carries: exports of builds and data are far below, and the
    /// whole message sits in memory twice (string and bytes).
    /// </summary>
    public const int MaxBytes = 20 * 1024 * 1024;

    /// <summary>Length of <see cref="MaxBytes"/> once encoded in base64.</summary>
    public const int MaxEncodedLength = (MaxBytes + 2) / 3 * 4;

    private const int BitsPerBase64Character = 6;
    private const int BitsPerByte = 8;

    /// <returns>Null when decoded; the bridge error code otherwise.</returns>
    public static string? TryDecode(string? base64, out byte[] content)
    {
        content = [];
        if (base64 is null)
        {
            return BridgeErrors.InvalidContent;
        }

        if (base64.Length > MaxEncodedLength)
        {
            return BridgeErrors.TooLarge;
        }

        var buffer = new byte[base64.Length * BitsPerBase64Character / BitsPerByte];
        if (!Convert.TryFromBase64String(base64, buffer, out var length))
        {
            return BridgeErrors.InvalidContent;
        }

        content = buffer[..length];
        return null;
    }
}
