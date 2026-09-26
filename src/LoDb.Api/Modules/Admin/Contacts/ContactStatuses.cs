namespace LoDb.Api.Modules.Admin.Contacts;

/// <summary>The states of a contact message, stored as the legacy stack stores them.</summary>
internal static class ContactStatuses
{
    public const string New = "new";
    public const string Handled = "handled";

    /// <summary>The status to filter on; null for both or any other value.</summary>
    public static string? Parse(string? status) => status is New or Handled ? status : null;
}
