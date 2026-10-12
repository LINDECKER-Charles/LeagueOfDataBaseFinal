namespace LoDb.Infrastructure.Persistence.Roles;

/// <summary>The passwords <c>migrate</c> may set on a role.</summary>
internal static class RolePasswords
{
    // Printable ASCII: the SCRAM verifier computed here then matches the client's proof,
    // without the SASLprep normalization PostgreSQL applies to other characters.
    private const char FirstPrintable = ' ';
    private const char LastPrintable = '~';

    public static bool IsPrintableAscii(string password) =>
        password.All(static character => character is >= FirstPrintable and <= LastPrintable);
}
