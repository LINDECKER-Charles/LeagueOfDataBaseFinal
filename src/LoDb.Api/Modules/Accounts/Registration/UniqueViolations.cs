using LoDb.Api.Modules.Accounts.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoDb.Api.Modules.Accounts.Registration;

/// <summary>
/// The field a unique index of <c>users</c> refused: two sign-ups racing for the same name
/// pass the checks made before the insert, and only the database tells them apart.
/// </summary>
internal static class UniqueViolations
{
    private const string EmailIndex = "uniq_users_email_lower";
    private const string UsernameIndex = "uniq_users_username_lower";

    /// <summary>
    /// <see cref="AccountFields.Email"/> or <see cref="AccountFields.Username"/> when
    /// <paramref name="exception"/> is a violation of their index; null otherwise.
    /// </summary>
    public static string? FieldOf(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        } violation
            ? violation.ConstraintName switch
            {
                EmailIndex => AccountFields.Email,
                UsernameIndex => AccountFields.Username,
                _ => null,
            }
            : null;

    /// <summary>The code of a taken <paramref name="field"/>.</summary>
    public static string TakenCode(string field) =>
        field == AccountFields.Email
            ? RegistrationRules.EmailTaken
            : RegistrationRules.UsernameTaken;
}
