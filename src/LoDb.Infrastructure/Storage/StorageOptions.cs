namespace LoDb.Infrastructure.Storage;

/// <summary>
/// Settings of the storage, bound from <c>LoDb:Storage</c>.
/// </summary>
internal sealed class StorageOptions
{
    public const string SectionName = "LoDb:Storage";

    /// <summary>
    /// Absolute directory holding blobs, datasets and the staging area. It must exist: the
    /// stores never create it, so a missing volume shows up instead of filling the container.
    /// </summary>
    public string Root { get; set; } = string.Empty;
}
