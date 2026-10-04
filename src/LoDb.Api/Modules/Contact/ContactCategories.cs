using System.Collections.Frozen;

namespace LoDb.Api.Modules.Contact;

/// <summary>
/// Reasons of a contact message, the legacy <c>ContactCategory</c>: the codes the form sends
/// and <c>contact_messages.category</c> stores.
/// </summary>
internal static class ContactCategories
{
    public const string Bug = "bug";
    public const string Feedback = "feedback";
    public const string Review = "review";
    public const string Commercial = "commercial";

    /// <summary>Every code, in the order of the form.</summary>
    public static IReadOnlyList<string> All { get; } = [Bug, Feedback, Review, Commercial];

    private static readonly FrozenSet<string> Known = All.ToFrozenSet(StringComparer.Ordinal);

    public static bool IsKnown(string code) => Known.Contains(code);
}
