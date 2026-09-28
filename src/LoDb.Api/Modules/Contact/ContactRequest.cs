namespace LoDb.Api.Modules.Contact;

/// <summary>Body of <c>POST /api/contact</c>.</summary>
internal sealed record ContactRequest
{
    /// <summary>bug, feedback, review or commercial.</summary>
    public string? Category { get; init; }

    /// <summary>Name of the visitor, optional, 120 characters at most.</summary>
    public string? Name { get; init; }

    /// <summary>Address the team replies to, 255 characters at most.</summary>
    public string? Email { get; init; }

    /// <summary>Subject, optional, 160 characters at most.</summary>
    public string? Subject { get; init; }

    /// <summary>The message, from 10 to 5,000 characters once trimmed.</summary>
    public string? Message { get; init; }

    /// <summary>
    /// Code of the locale of the page the visitor wrote from, such as <c>fr</c>; an unknown
    /// one is dropped, as it only informs the team.
    /// </summary>
    public string? Locale { get; init; }

    /// <summary>
    /// Honeypot, hidden from people: filled in, the message is acknowledged as sent and
    /// dropped, so that a robot learns nothing.
    /// </summary>
    public string? Website { get; init; }
}
