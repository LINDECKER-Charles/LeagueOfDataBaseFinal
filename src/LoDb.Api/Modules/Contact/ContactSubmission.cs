using LoDb.Domain.Languages;

namespace LoDb.Api.Modules.Contact;

/// <summary>A valid message, trimmed, its blank optional fields turned to null.</summary>
internal sealed record ContactSubmission
{
    /// <summary>One of <see cref="ContactCategories.All"/>.</summary>
    public required string Category { get; init; }

    public string? Name { get; init; }

    public required string Email { get; init; }

    public string? Subject { get; init; }

    public required string Message { get; init; }

    public UiLocale? Locale { get; init; }

    /// <summary>The code of the locale, as its URL segment writes it, if any.</summary>
    public string? LocaleCode => Locale is { } locale ? UiLocales.Code(locale) : null;
}
