namespace LoDb.Api.Modules.Accounts.Http;

/// <summary>
/// Settings of the journal's sign-in identifiers (<c>LoDb:Audit</c>).
/// </summary>
internal sealed class AuditIdentifierOptions
{
    public const string SectionName = "LoDb:Audit";

    /// <summary>
    /// Key of the digest that stands for an identifier typed on a failed sign-in, a secret of
    /// the host. Without one, each process draws its own: the digests of one identifier then
    /// match within a process only.
    /// </summary>
    public string? IdentifierKey { get; set; }
}
