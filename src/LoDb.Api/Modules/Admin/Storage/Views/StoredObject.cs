namespace LoDb.Api.Modules.Admin.Storage.Views;

/// <summary>An object of the storage root.</summary>
internal sealed record StoredObject
{
    /// <summary>Its path from the root, with forward slashes.</summary>
    public required string Path { get; init; }

    public required long Bytes { get; init; }
}
