using LoDb.Api.Modules.Admin.Http;

namespace LoDb.Api.Modules.Admin.Contacts;

/// <summary>A message of the contact form, as the admin reads it.</summary>
internal sealed record AdminContactRow
{
    public required int Id { get; init; }

    /// <summary>bug, feedback, review or commercial.</summary>
    public required string Category { get; init; }

    public string? Name { get; init; }

    /// <summary>The address to answer to.</summary>
    public required string Email { get; init; }

    public string? Subject { get; init; }

    public required string Message { get; init; }

    public string? Locale { get; init; }

    /// <summary>new or handled.</summary>
    public required string Status { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? HandledAt { get; init; }

    /// <summary>The account that sent it while signed in; null otherwise.</summary>
    public AdminUserRef? User { get; init; }
}
