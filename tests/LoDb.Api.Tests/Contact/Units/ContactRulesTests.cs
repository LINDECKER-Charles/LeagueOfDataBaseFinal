using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Contact;

namespace LoDb.Api.Tests.Contact.Units;

/// <summary>
/// The rules of the legacy contact form: a known category, an e-mail with a dotted domain, a
/// message of 10 to 5,000 characters, and a name, a subject and an e-mail kept to their
/// columns, lengths counted in characters as PHP counted them.
/// </summary>
public sealed class ContactRulesTests
{
    private const string Domain = "@example.test";

    // A crab: two UTF-16 units, one character.
    private const string Emoji = "\U0001F980";

    public static TheoryData<string, string, string> BrokenFields => new()
    {
        { ContactFields.Email, "pas-une-adresse", ContactRules.Invalid },
        { ContactFields.Email, "visiteur@localhost", ContactRules.Invalid },
        { ContactFields.Message, "  Neuf car.  ", ContactRules.TooShort },
        { ContactFields.Category, "sales", ContactRules.Invalid },
        {
            ContactFields.Name,
            new string('a', ContactRules.MaxNameLength + 1),
            ContactRules.TooLong
        },
        {
            ContactFields.Subject,
            new string('a', ContactRules.MaxSubjectLength + 1),
            ContactRules.TooLong
        },
        {
            ContactFields.Message,
            new string('a', ContactRules.MaxMessageLength + 1),
            ContactRules.TooLong
        },
        { ContactFields.Email, Address(ContactRules.MaxEmailLength + 1), ContactRules.TooLong },
    };

    [Theory]
    [MemberData(nameof(BrokenFields))]
    public void BrokenFieldIsNamedAlone(string field, string value, string code)
    {
        var errors = new FieldErrors();

        var submission = ContactRules.Read(With(field, value), errors);

        Assert.Null(submission);
        var refused = Assert.Single(errors.ToProblem().Errors!);
        Assert.Equal(field, refused.Key);
        Assert.Equal([code], refused.Value);
    }

    [Fact]
    public void FieldsAtTheirBoundsAreKept()
    {
        var request = Valid() with
        {
            Name = new string('a', ContactRules.MaxNameLength),
            Email = Address(ContactRules.MaxEmailLength),
            Subject = new string('a', ContactRules.MaxSubjectLength),
            Message = new string('a', ContactRules.MaxMessageLength),
        };

        Assert.Equal(request.Message, ContactRules.Read(request, new FieldErrors())?.Message);
    }

    [Fact]
    public void LengthsCountCharactersNotCodeUnits()
    {
        var shortest = string.Concat(Enumerable.Repeat(Emoji, ContactRules.MinMessageLength));
        var errors = new FieldErrors();

        var kept = ContactRules.Read(Valid() with { Message = shortest }, new FieldErrors());
        ContactRules.Read(Valid() with { Message = shortest[..^Emoji.Length] }, errors);

        Assert.Equal(shortest, kept?.Message);
        Assert.Equal([ContactRules.TooShort], errors.ToProblem().Errors![ContactFields.Message]);
    }

    private static ContactRequest Valid() => new()
    {
        Category = ContactCategories.Bug,
        Name = "Ahri Fan",
        Email = "visiteur@example.test",
        Subject = "Runes",
        Message = "La page des runes ne charge plus depuis ce matin.",
        Locale = "fr",
    };

    private static ContactRequest With(string field, string value) => field switch
    {
        ContactFields.Category => Valid() with { Category = value },
        ContactFields.Name => Valid() with { Name = value },
        ContactFields.Email => Valid() with { Email = value },
        ContactFields.Subject => Valid() with { Subject = value },
        ContactFields.Message => Valid() with { Message = value },
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
    };

    // A valid address of exactly `length` characters.
    private static string Address(int length) =>
        new string('v', length - Domain.Length) + Domain;
}
