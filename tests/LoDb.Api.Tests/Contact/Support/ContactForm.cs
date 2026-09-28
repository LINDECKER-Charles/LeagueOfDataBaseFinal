namespace LoDb.Api.Tests.Contact.Support;

/// <summary>The path of the form and a message it accepts.</summary>
public static class ContactForm
{
    public const string Path = "/api/contact";

    public const string Message = "La page des runes ne charge plus depuis ce matin.";

    /// <summary>A valid body, as the dialog of the footer sends it.</summary>
    public static Dictionary<string, object?> Valid() => new(StringComparer.Ordinal)
    {
        ["category"] = "bug",
        ["name"] = "Ahri Fan",
        ["email"] = "visiteur@example.test",
        ["subject"] = "Runes",
        ["message"] = Message,
        ["locale"] = "fr",
        ["website"] = string.Empty,
    };

    /// <summary><see cref="Valid"/> with <paramref name="field"/> set to another value.</summary>
    public static Dictionary<string, object?> With(string field, object? value)
    {
        var body = Valid();
        body[field] = value;
        return body;
    }
}
