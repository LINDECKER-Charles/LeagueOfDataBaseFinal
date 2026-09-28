using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Infrastructure.Persistence.Contact;

/// <summary>A row of <c>contact_messages</c>: a message sent through the contact form.</summary>
public sealed class ContactMessage
{
    public int Id { get; set; }

    /// <summary>bug, feedback, review or commercial.</summary>
    public required string Category { get; set; }

    public string? Name { get; set; }

    public required string Email { get; set; }

    public string? Subject { get; set; }

    public required string Message { get; set; }

    public string? Locale { get; set; }

    public string? Ip { get; set; }

    /// <summary>new or handled.</summary>
    public required string Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? HandledAt { get; set; }

    /// <summary>The signed-in sender, if any; null once the account is deleted.</summary>
    public int? UserId { get; set; }

    public User? User { get; set; }
}
