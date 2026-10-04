namespace LoDb.Api.Modules.Admin.Contacts;

/// <summary>A page of the contact messages, newest first.</summary>
internal sealed record AdminContactPage
{
    public required AdminContactStats Stats { get; init; }

    public required IReadOnlyList<AdminContactRow> Items { get; init; }

    /// <summary>Messages of the status asked for.</summary>
    public required int Total { get; init; }

    /// <summary>The page, from 1.</summary>
    public required int Page { get; init; }

    public required int Pages { get; init; }
}
