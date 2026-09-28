namespace LoDb.Api.Modules.Contact;

/// <summary>
/// Names of the fields of the form as the JSON writes them, the keys of <c>errors</c> in a
/// validation problem.
/// </summary>
internal static class ContactFields
{
    public const string Category = "category";
    public const string Name = "name";
    public const string Email = "email";
    public const string Subject = "subject";
    public const string Message = "message";
}
