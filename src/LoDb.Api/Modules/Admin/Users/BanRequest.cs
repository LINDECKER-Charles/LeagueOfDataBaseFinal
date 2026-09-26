namespace LoDb.Api.Modules.Admin.Users;

/// <summary>A ban, and why: the reason is optional and shown in the list.</summary>
internal sealed record BanRequest
{
    /// <summary>At most 255 characters; blank for none.</summary>
    public string? Reason { get; init; }
}
