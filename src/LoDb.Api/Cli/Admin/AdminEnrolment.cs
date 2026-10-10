using System.Globalization;

namespace LoDb.Api.Cli.Admin;

/// <summary>
/// What an administrator does to enrol an authenticator, as <c>admin create</c> and
/// <c>admin mfa-invite</c> print it around the invitation.
/// </summary>
internal static class AdminEnrolment
{
    /// <summary>The page of the enrolment, which reads the invitation from its query.</summary>
    public const string EnrollPath = "/admin/enroll";

    public const string InvitationParameter = "invitation";

    private const string WholeMinutes = "0";

    /// <summary>The line that carries the invitation, the one a script reads.</summary>
    public static string InvitationLine(string invitation, TimeSpan lifetime)
    {
        var minutes = lifetime.TotalMinutes.ToString(WholeMinutes, CultureInfo.InvariantCulture);
        return $"Invitation to enrol an authenticator, valid {minutes} minutes: {invitation}";
    }

    /// <summary>The steps of the enrolment the invitation opens.</summary>
    public static IEnumerable<string> Steps(string invitation) =>
    [
        $"Open {EnrollPath}?{InvitationParameter}={invitation} and sign in with this e-mail "
            + "and its password.",
        "Scan the QR code with an authenticator app, then enter its code.",
        "Keep the recovery codes, shown once, away from the authenticator.",
    ];
}
